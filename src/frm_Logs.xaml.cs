using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using SwiftExcel; // KHAI BÁO THƯ VIỆN SWIFTEXCEL Ở ĐÂY

namespace SCADA_VERTEX
{
    /// <summary>
    /// Interaction logic for frm_Logs.xaml
    /// </summary>
    public partial class frm_Logs : UserControl
    {
        string connString = Properties.Settings.Default.SQL_String;

        public frm_Logs()
        {
            InitializeComponent();

            // Setup mặc định khi vừa mở tab Log: Lọc từ đầu ngày đến cuối ngày hôm nay
            dtpFrom.SelectedDate = DateTime.Today;
            txtFromTime.Text = "00:00";

            dtpTo.SelectedDate = DateTime.Today;
            txtToTime.Text = "23:59";

            LoadDataLog();
        }

        // ============================================================
        // 1. HÀM TẢI VÀ LỌC DỮ LIỆU TỪ SQL (HIỂN THỊ ĐỦ CÁC CỘT MỚI)
        // ============================================================
        private void LoadDataLog()
        {
            if (string.IsNullOrEmpty(connString)) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            ROW_NUMBER() OVER(ORDER BY L.TimeScan DESC) AS No,
                            L.BatchName AS BatchName,
                            L.Barcode AS Barcode,
                            ISNULL(P.PatientName, 'Unknown') AS Name,
                            L.TestType AS Type,
                            L.Result AS Result,
                            L.StartTime AS StartTime,
                            L.TimeScan AS TimeScan,
                            ROUND(L.DurationSec, 2) AS DurationSec
                        FROM Tbl_DataLog L
                        LEFT JOIN Tbl_PatientProfile P ON L.Barcode = P.SampleID
                        WHERE 1=1 "; ;

                    // --- 1. LỌC THEO THỜI GIAN (FROM - TO) ---
                    if (dtpFrom.SelectedDate.HasValue)
                        query += " AND L.TimeScan >= @fromDate ";

                    if (dtpTo.SelectedDate.HasValue)
                        query += " AND L.TimeScan <= @toDate ";

                    // --- 2. LỌC THEO BARCODE ---
                    if (!string.IsNullOrWhiteSpace(txtSearchBarcode.Text))
                        query += " AND L.Barcode LIKE @barcode ";

                    // --- 3. LỌC THEO LOẠI XÉT NGHIỆM (COMBOBOX TEST TYPE) ---
                    string typeFilter = (cmbSearchTestType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
                    if (typeFilter != "All")
                    {
                        query += " AND L.TestType LIKE @testType ";
                    }

                    // --- 4. LỌC THEO KẾT QUẢ (COMBOBOX RESULT) ---
                    string resultFilter = (cmbSearchResult.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
                    if (resultFilter == "OK") query += " AND L.Result LIKE 'OK%' ";
                    else if (resultFilter == "Error") query += " AND L.Result LIKE 'Error%' ";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (dtpFrom.SelectedDate.HasValue)
                        {
                            DateTime fromDate = dtpFrom.SelectedDate.Value.Date;
                            TimeSpan.TryParse(txtFromTime.Text + ":00", out TimeSpan fromTime);
                            cmd.Parameters.AddWithValue("@fromDate", fromDate.Add(fromTime));
                        }

                        if (dtpTo.SelectedDate.HasValue)
                        {
                            DateTime toDate = dtpTo.SelectedDate.Value.Date;
                            if (!TimeSpan.TryParse(txtToTime.Text + ":59", out TimeSpan toTime))
                                toTime = new TimeSpan(23, 59, 59);
                            cmd.Parameters.AddWithValue("@toDate", toDate.Add(toTime));
                        }

                        if (!string.IsNullOrWhiteSpace(txtSearchBarcode.Text))
                        {
                            cmd.Parameters.AddWithValue("@barcode", "%" + txtSearchBarcode.Text.Trim() + "%");
                        }

                        // Truyền tham số lọc Test Type vào SQL
                        if (typeFilter != "All")
                        {
                            cmd.Parameters.AddWithValue("@testType", "%" + typeFilter + "%");
                        }

                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        // Gán nguồn dữ liệu cho DataGrid hiển thị
                        dgvDataLog.ItemsSource = dt.DefaultView;
                        UpdateStatistics(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải DataLog:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // 2. HÀM THỐNG KÊ (TOTAL, OK, ERR)
        // ============================================================
        private void UpdateStatistics(DataTable dt)
        {
            int total = dt.Rows.Count;
            int errCount = 0;
            int okCount = 0;

            foreach (DataRow row in dt.Rows)
            {
                string res = row["Result"].ToString() ?? "";
                if (res.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                    errCount++;
                else if (res.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    okCount++;
            }

            txtTotalCount.Text = total.ToString();
            txtOkCount.Text = okCount.ToString();
            txtErrCount.Text = errCount.ToString();
        }

        // ============================================================
        // 3. CÁC NÚT ĐIỀU KHIỂN
        // ============================================================
        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            LoadDataLog();
        }

        private void btnToday_Click(object sender, RoutedEventArgs e)
        {
            dtpFrom.SelectedDate = DateTime.Today;
            txtFromTime.Text = "00:00";

            dtpTo.SelectedDate = DateTime.Today;
            txtToTime.Text = "23:59";

            txtSearchBarcode.Text = "";
            cmbSearchResult.SelectedIndex = 0;
            if (cmbSearchTestType != null) cmbSearchTestType.SelectedIndex = 0; // Reset cả loại xét nghiệm

            LoadDataLog();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            dtpFrom.SelectedDate = null;
            txtFromTime.Text = "00:00";
            dtpTo.SelectedDate = null;
            txtToTime.Text = "23:59";
            txtSearchBarcode.Text = "";
            cmbSearchResult.SelectedIndex = 0;
            if (cmbSearchTestType != null) cmbSearchTestType.SelectedIndex = 0;

            // ĐÁNH SẬP DỮ LIỆU BẢNG VÀ RESET BỘ ĐẾM VỀ 0
            dgvDataLog.ItemsSource = null;
            txtTotalCount.Text = "0";
            txtOkCount.Text = "0";
            txtErrCount.Text = "0";
        }

        // ============================================================
        // 4. XUẤT EXCEL BẰNG SWIFTEXCEL (CHỈ XUẤT KẾT QUẢ ĐANG LỌC)
        // ============================================================
        private void btnExportCSV_Click(object sender, RoutedEventArgs e)
        {
            if (dgvDataLog.ItemsSource == null) return;

            // Lấy trực tiếp DataView đang hiển thị trên giao diện (ĐÃ BỊ LỌC THEO TEST TYPE, RESULT, TIME)
            DataView view = (DataView)dgvDataLog.ItemsSource;
            DataTable dt = view.ToTable(); // ToTable() đảm bảo xuất chính xác các dòng đang được lọc

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu để xuất!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "Excel file (*.xlsx)|*.xlsx";
            saveFileDialog.FileName = "SCADA_DataLog_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Khởi tạo Sheet
                    var sheet = new Sheet
                    {
                        Name = "DataLog"
                    };

                    // Sử dụng ExcelWriter của SwiftExcel ghi trực tiếp luồng dữ liệu
                    using (var excelWriter = new ExcelWriter(saveFileDialog.FileName, sheet))
                    {
                        // 1. Ghi Header (Tiêu đề cột)
                        for (int col = 0; col < dt.Columns.Count; col++)
                        {
                            // Cột và Dòng trong SwiftExcel bắt đầu từ số 1
                            excelWriter.Write(dt.Columns[col].ColumnName, col + 1, 1);
                        }

                        // 2. Ghi Dữ liệu từng dòng
                        for (int row = 0; row < dt.Rows.Count; row++)
                        {
                            for (int col = 0; col < dt.Columns.Count; col++)
                            {
                                string cellValue = dt.Rows[row][col].ToString() ?? "";
                                excelWriter.Write(cellValue, col + 1, row + 2); // row + 2 vì dòng 1 là header
                            }
                        }
                    }

                    MessageBox.Show("Xuất file Excel thành công!\nFile chỉ chứa đúng dữ liệu bạn vừa lọc.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi xuất file Excel:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}