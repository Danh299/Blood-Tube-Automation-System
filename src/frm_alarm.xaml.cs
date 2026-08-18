using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using SwiftExcel; // Cần dùng để export Excel

namespace SCADA_VERTEX
{
    public partial class frm_alarm : UserControl
    {
        internal static frm_alarm? Data_transfer;
        string connString = Properties.Settings.Default.SQL_String;

        // Định nghĩa một Object cho Current Alarm
        public class CurrentAlarmItem
        {
            public string Time { get; set; } = "";
            public string AlarmName { get; set; } = "";
            public string Solution { get; set; } = "";
            public string AlarmCode { get; set; } = "";
        }

        // Collection để liên kết tự động tới DataGrid Current
        public ObservableCollection<CurrentAlarmItem> CurrentAlarms { get; set; } = new ObservableCollection<CurrentAlarmItem>();

        public frm_alarm()
        {
            InitializeComponent();
            Data_transfer = this;

            // Gán dữ liệu cho bảng Current Alarm
            dgvCurrentAlarm.ItemsSource = CurrentAlarms;

            // Setup thời gian mặc định cho History
            dtpAlarmFrom.SelectedDate = DateTime.Today;
            dtpAlarmTo.SelectedDate = DateTime.Today;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Nếu tab History đang mở, Load dữ liệu khi vào tab
            if (bdrQuery.Visibility == Visibility.Visible)
            {
                LoadHistoryAlarms();
            }
        }

        // ============================================================
        // 1. CẬP NHẬT CURRENT ALARM (Được gọi liên tục từ MainWindow)
        // ============================================================
        public void AddCurrentAlarm(string name, string solution, string code)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Kiểm tra xem lỗi đã có trên bảng chưa, chưa có thì thêm vào
                bool exists = false;
                foreach (var item in CurrentAlarms)
                {
                    if (item.AlarmCode == code)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    CurrentAlarms.Add(new CurrentAlarmItem
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        AlarmName = name,
                        Solution = solution,
                        AlarmCode = code
                    });
                }
            });
        }

        public void RemoveCurrentAlarm(string code)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Xóa lỗi khỏi bảng nếu có
                for (int i = 0; i < CurrentAlarms.Count; i++)
                {
                    if (CurrentAlarms[i].AlarmCode == code)
                    {
                        CurrentAlarms.RemoveAt(i);
                        break;
                    }
                }
            });
        }

        // ============================================================
        // 2. CHUYỂN ĐỔI TAB GIAO DIỆN
        // ============================================================
        private void btnTabCurrent_Click(object sender, RoutedEventArgs e)
        {
            btnTabCurrent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00A2E8"));
            btnTabCurrent.Foreground = Brushes.White;
            btnTabHistory.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A6A6A6"));
            btnTabHistory.Foreground = Brushes.Black;

            txtModeTitle.Text = "CURRENT ALARM MONITOR";
            bdrQuery.Visibility = Visibility.Collapsed;
            dgvCurrentAlarm.Visibility = Visibility.Visible;
            dgvAlarmLog.Visibility = Visibility.Collapsed;
        }

        private void btnTabHistory_Click(object sender, RoutedEventArgs e)
        {
            btnTabHistory.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00A2E8"));
            btnTabHistory.Foreground = Brushes.White;
            btnTabCurrent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A6A6A6"));
            btnTabCurrent.Foreground = Brushes.Black;

            txtModeTitle.Text = "HISTORY ALARM QUERY";
            bdrQuery.Visibility = Visibility.Visible;
            dgvCurrentAlarm.Visibility = Visibility.Collapsed;
            dgvAlarmLog.Visibility = Visibility.Visible;

            LoadHistoryAlarms();
        }

        // ============================================================
        // 3. XỬ LÝ LỊCH SỬ TỪ CƠ SỞ DỮ LIỆU
        // ============================================================
        private void LoadHistoryAlarms()
        {
            if (string.IsNullOrEmpty(connString)) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            ROW_NUMBER() OVER(ORDER BY Time DESC) AS No,
                            Time, AlarmName, Solution, AlarmCode
                        FROM Tbl_AlarmLog 
                        WHERE 1=1 ";

                    if (dtpAlarmFrom.SelectedDate.HasValue)
                        query += " AND Time >= @fromDate ";
                    if (dtpAlarmTo.SelectedDate.HasValue)
                        query += " AND Time <= @toDate ";
                    if (!string.IsNullOrWhiteSpace(txtAlarmKeyword.Text))
                        query += " AND (AlarmName LIKE @keyword OR AlarmCode LIKE @keyword) ";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (dtpAlarmFrom.SelectedDate.HasValue)
                        {
                            DateTime fromDate = dtpAlarmFrom.SelectedDate.Value.Date;
                            TimeSpan.TryParse(txtAlarmFromTime.Text + ":00", out TimeSpan fromTime);
                            cmd.Parameters.AddWithValue("@fromDate", fromDate.Add(fromTime));
                        }

                        if (dtpAlarmTo.SelectedDate.HasValue)
                        {
                            DateTime toDate = dtpAlarmTo.SelectedDate.Value.Date;
                            if (!TimeSpan.TryParse(txtAlarmToTime.Text + ":59", out TimeSpan toTime))
                                toTime = new TimeSpan(23, 59, 59);
                            cmd.Parameters.AddWithValue("@toDate", toDate.Add(toTime));
                        }

                        if (!string.IsNullOrWhiteSpace(txtAlarmKeyword.Text))
                        {
                            cmd.Parameters.AddWithValue("@keyword", "%" + txtAlarmKeyword.Text.Trim() + "%");
                        }

                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        dgvAlarmLog.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải Alarm History:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnQueryAlarm_Click(object sender, RoutedEventArgs e)
        {
            LoadHistoryAlarms();
        }

        // ============================================================
        // 4. XUẤT EXCEL BẰNG SWIFTEXCEL
        // ============================================================
        private void btnExportAlarm_Click(object sender, RoutedEventArgs e)
        {
            if (dgvAlarmLog.ItemsSource == null) return;

            DataView view = (DataView)dgvAlarmLog.ItemsSource;
            DataTable dt = view.Table!;

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu lịch sử Alarm để xuất!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "Excel file (*.xlsx)|*.xlsx";
            saveFileDialog.FileName = "Alarm_History_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var sheet = new Sheet { Name = "AlarmHistory" };

                    using (var excelWriter = new ExcelWriter(saveFileDialog.FileName, sheet))
                    {
                        for (int col = 0; col < dt.Columns.Count; col++)
                        {
                            excelWriter.Write(dt.Columns[col].ColumnName, col + 1, 1);
                        }

                        for (int row = 0; row < dt.Rows.Count; row++)
                        {
                            for (int col = 0; col < dt.Columns.Count; col++)
                            {
                                string cellValue = dt.Rows[row][col].ToString() ?? "";
                                excelWriter.Write(cellValue, col + 1, row + 2);
                            }
                        }
                    }
                    MessageBox.Show("Xuất file Excel thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi xuất file Excel:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}