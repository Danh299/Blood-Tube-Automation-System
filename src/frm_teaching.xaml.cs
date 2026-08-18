using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;

namespace SCADA_VERTEX
{
    /// <summary>
    /// Interaction logic for frm_teaching.xaml
    /// </summary>
    public partial class frm_teaching : UserControl
    {
        internal static frm_teaching? Data_transfer;

        // 1. CẤU TRÚC LƯU TRỮ DỮ LIỆU ĐIỂM
        public class RobotPoint
        {
            public string PointName { get; set; } = "";
            public string X { get; set; } = "0";
            public string Y { get; set; } = "0";
            public string Z { get; set; } = "0";
            public string R { get; set; } = "0";
            public string VelX { get; set; } = "100";
            public string VelY { get; set; } = "100";
            public string VelZ { get; set; } = "100";
            public string VelR { get; set; } = "100";
        }

        public string CenVel { get; set; } = "0";
        public string CenTime { get; set; } = "0";

        #region 1. Các hàm tọa độ robot
        // Danh sách hiển thị lên DataGrid
        public ObservableCollection<RobotPoint> RobotPoints { get; set; }
        public frm_teaching()
        {
            InitializeComponent();
            Data_transfer = this;
            LoadDataFromSQL();
        }

        // Hàm lọc và ép chuẩn định dạng 2 số thập phân
        private string FormatToF2(object dbValue)
        {
            if (dbValue != null && double.TryParse(dbValue.ToString(), out double val))
            {
                return val.ToString("F2"); // Ép về đúng 2 số lẻ (VD: 12.5 -> 12.50)
            }
            return "0.00"; // Giá trị phòng hờ nếu Database bị NULL hoặc lỗi chữ
        }

        // ==========================================
        // HÀM LOAD DỮ LIỆU TỪ SQL LÊN DATAGRID
        // ==========================================
        private void LoadDataFromSQL()
        {
            RobotPoints = new ObservableCollection<RobotPoint>();
            string connString = Properties.Settings.Default.SQL_String;

            // Nếu chưa cài đặt chuỗi kết nối ở Tab Setting thì thoát luôn
            if (string.IsNullOrEmpty(connString)) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    string query = "SELECT * FROM tb_RobotPoints";
                    string queryCen = "SELECT * FROM tb_Centrifuge WHERE SettingID = 1";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                RobotPoints.Add(new RobotPoint
                                {
                                    PointName = reader["PointName"]?.ToString() ?? "",
                                    X = FormatToF2(reader["PosX"]),
                                    Y = FormatToF2(reader["PosY"]),
                                    Z = FormatToF2(reader["PosZ"]),
                                    R = FormatToF2(reader["PosR"]),
                                    VelX = reader["VelX"]?.ToString() ?? "0",
                                    VelY = reader["VelY"]?.ToString() ?? "0",
                                    VelZ = reader["VelZ"]?.ToString() ?? "0",
                                    VelR = reader["VelR"]?.ToString() ?? "0"
                                });
                            }
                        }
                    }
                    using (SqlCommand cmdCen = new SqlCommand(queryCen, conn))
                    {
                        using (SqlDataReader readerCen = cmdCen.ExecuteReader())
                        {
                            if (readerCen.Read())
                            {
                                CenVel = readerCen["Velocity"]?.ToString() ?? "0";
                                CenTime = readerCen["SpinTime"]?.ToString() ?? "0";
                                txtCenVel.Text = CenVel;
                                txtCenTime.Text = CenTime;
                            }
                        }
                    }
                }
                dgvRobotPoints.ItemsSource = RobotPoints; // Đổ dữ liệu vào bảng
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to load SQL data:\n" + ex.Message, "Database Loading Error");
            }
        }

        // ==========================================
        // 1. NÚT TEACH: LƯU VỊ TRÍ HIỆN TẠI VÀO BẢNG
        // ==========================================
        private void btnTeachPoint_Click(object sender, RoutedEventArgs e)
        {
            // Lấy dòng đang được click chọn trên DataGrid
            var selectedRow = dgvRobotPoints.SelectedItem as RobotPoint;

            if (selectedRow == null)
            {
                MessageBox.Show("Please click on a row in the table (e.g., BeltUp) before clicking Teach!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ghi đè tọa độ thực tế vào dòng đó
            selectedRow.X = txtPosX.Text;
            selectedRow.Y = txtPosY.Text;
            selectedRow.Z = txtPosZ.Text;
            selectedRow.R = txtPosR.Text;

            // Refresh lại bảng để nó hiện số mới lên giao diện
            dgvRobotPoints.Items.Refresh();
        }

        // ==========================================
        // 2. NÚT SAVE: LƯU TOÀN BỘ BẢNG LÊN SQL
        // ==========================================
        private void btnSaveDB_Click(object sender, RoutedEventArgs e)
        {
            if (RobotPoints == null || RobotPoints.Count == 0) return;

            string connString = Properties.Settings.Default.SQL_String;
            if (string.IsNullOrEmpty(connString))
            {
                MessageBox.Show("SQL is not installed. Please go to the Settings tab to configure it!", "Error");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();

                    // Duyệt qua 16 dòng trong bảng, đẩy từng lệnh Update xuống
                    foreach (var pt in RobotPoints)
                    {
                        string sqlUpdate = @"
                            UPDATE tb_RobotPoints 
                            SET PosX=@px, PosY=@py, PosZ=@pz, PosR=@pr, 
                                VelX=@vx, VelY=@vy, VelZ=@vz, VelR=@vr 
                            WHERE PointName=@name";

                        using (SqlCommand cmd = new SqlCommand(sqlUpdate, conn))
                        {
                            cmd.Parameters.AddWithValue("@px", float.Parse(pt.X));
                            cmd.Parameters.AddWithValue("@py", float.Parse(pt.Y));
                            cmd.Parameters.AddWithValue("@pz", float.Parse(pt.Z));
                            cmd.Parameters.AddWithValue("@pr", float.Parse(pt.R));

                            cmd.Parameters.AddWithValue("@vx", int.Parse(pt.VelX));
                            cmd.Parameters.AddWithValue("@vy", int.Parse(pt.VelY));
                            cmd.Parameters.AddWithValue("@vz", int.Parse(pt.VelZ));
                            cmd.Parameters.AddWithValue("@vr", int.Parse(pt.VelR));

                            cmd.Parameters.AddWithValue("@name", pt.PointName);

                            cmd.ExecuteNonQuery(); // Bóp cò thực thi lệnh
                        }
                    }
                }
                MessageBox.Show("16 points have been successfully saved to the SQL database!", "Save Database", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving to SQL:\n" + ex.Message, "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //// ==========================================
        //// 3. NÚT DOWNLOAD: ĐẨY DỮ LIỆU XUỐNG PLC (ĐÃ TỐI ƯU BẤT ĐỒNG BỘ)
        //// ==========================================
        //private async void btnDownloadPLC_Click(object sender, RoutedEventArgs e)
        //{
        //    var result = Class_Common.fn_Confirm();

        //    if (result == true)
        //    {
        //        try
        //        {
        //            // Clone danh sách để đọc an toàn dưới luồng nền (tránh xung đột với DataGrid)
        //            var pointsToDownload = RobotPoints.ToList();
        //            string currentCenTime = CenTime;
        //            string currentCenVel = CenVel;

        //            // Giải phóng giao diện bằng Task.Run
        //            await Task.Run(() =>
        //            {
        //                // ĐỊNH NGHĨA HỆ SỐ CƠ KHÍ 
        //                double k_Linear = 800.0 / 8.0;   // Trục X, Y, Z (Vít me 8mm) = 100 xung/mm
        //                double k_Rotary = 800.0 / 360.0; // Trục R (Xoay 360 độ) = ~2.22 xung/độ

        //                // gửi thông số ly tâm xuống PLC
        //                int Time_Cen = int.Parse(currentCenTime);
        //                int Vel_Cen = int.Parse(currentCenVel);

        //                // Lưu ý: Đã sửa Vel_Cen / 60.0 để tránh bị làm tròn bằng 0 trong C#
        //                int Fre_Cen = (int)((Vel_Cen / 60.0) * 800);
        //                int pulse_Cen = Time_Cen * Fre_Cen;

        //                MainWindow.Data_transfer.TagWrite("Time_Cen", pulse_Cen);
        //                MainWindow.Data_transfer.TagWrite("Vel_Cen", Fre_Cen);

        //                foreach (var pt in pointsToDownload)
        //                {
        //                    // 1. Chuyển đổi chuỗi (string) sang số thực (double)
        //                    double posX_mm = double.Parse(pt.X);
        //                    double posY_mm = double.Parse(pt.Y);
        //                    double posZ_mm = double.Parse(pt.Z);
        //                    double posR_deg = double.Parse(pt.R);

        //                    // 2. Tính toán số xung và ép kiểu về số nguyên 32-bit (int)
        //                    int targetPulseX = (int)Math.Round(posX_mm * k_Linear);
        //                    int targetPulseY = (int)Math.Round(posY_mm * k_Linear);
        //                    int targetPulseZ = (int)Math.Round(posZ_mm * k_Linear);
        //                    int targetPulseR = (int)Math.Round(posR_deg * k_Rotary);

        //                    // 3. Xử lý Vận tốc (Giả sử SQL đang lưu mm/s, cần đổi ra Hz)
        //                    int velHzX = (int)Math.Round(double.Parse(pt.VelX) * k_Linear);
        //                    int velHzY = (int)Math.Round(double.Parse(pt.VelY) * k_Linear);
        //                    int velHzZ = (int)Math.Round(double.Parse(pt.VelZ) * k_Linear);
        //                    int velHzR = (int)Math.Round(double.Parse(pt.VelR) * k_Rotary);

        //                    // 4. Bắn dữ liệu xuống Kepware (Gửi dưới dạng int - DWord)
        //                    if (pt.PointName == "BeltUp")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_BeltX", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_BeltY", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_BeltZ", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_BeltVelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_BeltVelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_BeltVelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "BeltDown")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_BeltDownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "CamUp")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_CamX", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_CamY", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_CamZ", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_CamVelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_CamVelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_CamVelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "CamDown")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_CamDownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "CenUp")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_CenX", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_CenY", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_CenZ", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_CenVelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_CenVelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_CenVelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "CenDown")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_CenDownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "DecapUp")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapX", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapY", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapZ", targetPulseZ);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapPosR", targetPulseR);

        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapVelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapVelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapVelZ", velHzZ);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapVelR", velHzR);
        //                    }
        //                    else if (pt.PointName == "DecapDown_N")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapDownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "DecapDown_KN")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_DecapDownZ_KN", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "Drop_N")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_DropX", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DropY", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DropZ", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_DropVelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DropVelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_DropVelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "Tray1_Up")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray1X", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray1Y", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray1Z", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray1VelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray1VelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray1VelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "Tray1_Down")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray1DownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "Tray2_Up")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray2X", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray2Y", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray2Z", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray2VelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray2VelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray2VelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "Tray2_Down")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray2DownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "Tray3_Up")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray3X", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray3Y", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray3Z", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray3VelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray3VelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray3VelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "Tray3_Down")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_Tray3DownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "TrayErr_Up")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayErrX", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayErrY", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayErrZ", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayErrVelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayErrVelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayErrVelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "TrayErr_Down")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayErrDownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "TrayTG_Up")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayTGX", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayTGY", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayTGZ", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayTGVelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayTGVelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayTGVelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "TrayTG_Down")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayTGDownZ", targetPulseZ);
        //                    }
        //                    else if (pt.PointName == "TrayOngBu_Up")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuX", targetPulseX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuY", targetPulseY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuZ", targetPulseZ);

        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuVelX", velHzX);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuVelY", velHzY);
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuVelZ", velHzZ);
        //                    }
        //                    else if (pt.PointName == "TrayOngBu_Down")
        //                    {
        //                        MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuDownZ", targetPulseZ);
        //                    }
        //                }
        //            });

        //            // Lệnh này được kích hoạt ở luồng UI sau khi Task background thực hiện xong
        //            MessageBox.Show("The formula has been successfully uploaded to the PLC. Ready to run automatically!", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show("PLC loading error:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //        }
        //    }
        //}


        // ==========================================
        // 3. NÚT DOWNLOAD: ĐẨY DỮ LIỆU XUỐNG PLC (ĐÃ TỐI ƯU BẤT ĐỒNG BỘ)
        // ==========================================

        // Đưa toàn bộ logic gửi PLC vào 1 hàm Public để các Tab khác có thể gọi ké
        public async Task Logic_Download_Coordinates_Async(bool showSuccessMsg = true)
        {
            try
            {
                // Clone danh sách để đọc an toàn dưới luồng nền
                var pointsToDownload = RobotPoints.ToList();
                string currentCenTime = CenTime;
                string currentCenVel = CenVel;

                // Giải phóng giao diện bằng Task.Run
                await Task.Run(() =>
                {
                    // ĐỊNH NGHĨA HỆ SỐ CƠ KHÍ 
                    double k_Linear = 800.0 / 8.0;   // Trục X, Y, Z (Vít me 8mm) = 100 xung/mm
                    double k_Rotary = 800.0 / 360.0; // Trục R (Xoay 360 độ) = ~2.22 xung/độ

                    // gửi thông số ly tâm xuống PLC
                    int Time_Cen = int.Parse(currentCenTime);
                    int Vel_Cen = int.Parse(currentCenVel);

                    int Fre_Cen = (int)((Vel_Cen / 60.0) * 800);
                    int pulse_Cen = Time_Cen * Fre_Cen;

                    MainWindow.Data_transfer!.TagWrite("Time_Cen", pulse_Cen);
                    MainWindow.Data_transfer.TagWrite("Vel_Cen", Fre_Cen);

                    foreach (var pt in pointsToDownload)
                    {
                        // 1. Chuyển đổi chuỗi sang số thực
                        double posX_mm = double.Parse(pt.X);
                        double posY_mm = double.Parse(pt.Y);
                        double posZ_mm = double.Parse(pt.Z);
                        double posR_deg = double.Parse(pt.R);

                        // 2. Tính toán số xung
                        int targetPulseX = (int)Math.Round(posX_mm * k_Linear);
                        int targetPulseY = (int)Math.Round(posY_mm * k_Linear);
                        int targetPulseZ = (int)Math.Round(posZ_mm * k_Linear);
                        int targetPulseR = (int)Math.Round(posR_deg * k_Rotary);

                        // 3. Xử lý Vận tốc
                        int velHzX = (int)Math.Round(double.Parse(pt.VelX) * k_Linear);
                        int velHzY = (int)Math.Round(double.Parse(pt.VelY) * k_Linear);
                        int velHzZ = (int)Math.Round(double.Parse(pt.VelZ) * k_Linear);
                        int velHzR = (int)Math.Round(double.Parse(pt.VelR) * k_Rotary);

                        // 4. Bắn dữ liệu xuống Kepware
                        if (pt.PointName == "BeltUp")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_BeltX", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_BeltY", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_BeltZ", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_BeltVelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_BeltVelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_BeltVelZ", velHzZ);
                        }
                        else if (pt.PointName == "BeltDown")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_BeltDownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "CamUp")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_CamX", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_CamY", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_CamZ", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_CamVelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_CamVelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_CamVelZ", velHzZ);
                        }
                        else if (pt.PointName == "CamDown")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_CamDownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "CenUp")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_CenX", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_CenY", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_CenZ", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_CenVelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_CenVelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_CenVelZ", velHzZ);
                        }
                        else if (pt.PointName == "CenDown")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_CenDownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "DecapUp")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_DecapX", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_DecapY", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_DecapZ", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_DecapPosR", targetPulseR);
                            MainWindow.Data_transfer.TagWrite("Tag_DecapVelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_DecapVelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_DecapVelZ", velHzZ);
                            MainWindow.Data_transfer.TagWrite("Tag_DecapVelR", velHzR);
                        }
                        else if (pt.PointName == "DecapDown_N")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_DecapDownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "DecapDown_KN")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_DecapDownZ_KN", targetPulseZ);
                        }
                        else if (pt.PointName == "Drop_N")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_DropX", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_DropY", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_DropZ", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_DropVelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_DropVelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_DropVelZ", velHzZ);
                        }
                        else if (pt.PointName == "Tray1_Up")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_Tray1X", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray1Y", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray1Z", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray1VelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray1VelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray1VelZ", velHzZ);
                        }
                        else if (pt.PointName == "Tray1_Down")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_Tray1DownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "Tray2_Up")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_Tray2X", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray2Y", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray2Z", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray2VelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray2VelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray2VelZ", velHzZ);
                        }
                        else if (pt.PointName == "Tray2_Down")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_Tray2DownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "Tray3_Up")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_Tray3X", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray3Y", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray3Z", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray3VelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray3VelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_Tray3VelZ", velHzZ);
                        }
                        else if (pt.PointName == "Tray3_Down")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_Tray3DownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "TrayErr_Up")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_TrayErrX", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayErrY", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayErrZ", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayErrVelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayErrVelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayErrVelZ", velHzZ);
                        }
                        else if (pt.PointName == "TrayErr_Down")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_TrayErrDownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "TrayTG_Up")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_TrayTGX", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayTGY", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayTGZ", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayTGVelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayTGVelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayTGVelZ", velHzZ);
                        }
                        else if (pt.PointName == "TrayTG_Down")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_TrayTGDownZ", targetPulseZ);
                        }
                        else if (pt.PointName == "TrayOngBu_Up")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuX", targetPulseX);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuY", targetPulseY);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuZ", targetPulseZ);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuVelX", velHzX);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuVelY", velHzY);
                            MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuVelZ", velHzZ);
                        }
                        else if (pt.PointName == "TrayOngBu_Down")
                        {
                            MainWindow.Data_transfer.TagWrite("Tag_TrayOngBuDownZ", targetPulseZ);
                        }
                    }
                });

                // Hiện thông báo thành công trên luồng chính
                if (showSuccessMsg)
                {
                    MessageBox.Show("Parameters and Coordinates have been successfully uploaded to the PLC!", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("PLC loading error:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnDownloadPLC_Click(object sender, RoutedEventArgs e)
        {
            var result = Class_Common.fn_Confirm();

            // Nếu người dùng chọn YES trên Form Teaching thì mới tải xuống
            if (result == true)
            {
                await Logic_Download_Coordinates_Async(true);
            }
        }

        #endregion

        #region 2. Các Hàm lưu thông số ly tâm

        // --- NÚT EDIT: MỞ KHÓA CHO PHÉP NHẬP ---
        private void btnCenEdit_Click(object sender, RoutedEventArgs e)
        {
            // Mở khóa nhập liệu
            txtCenVel.IsReadOnly = false;
            txtCenTime.IsReadOnly = false;

            // Đổi nền thành màu trắng báo hiệu có thể gõ
            txtCenVel.Background = Brushes.White;
            txtCenTime.Background = Brushes.White;

            // Vô hiệu hóa nút Edit, làm mờ đi
            btnCenEdit.IsEnabled = false;
            btnCenEdit.Background = (Brush)new BrushConverter().ConvertFrom("#A6A6A6")!;

            // Kích hoạt nút Save, đổi sang màu xanh lá
            btnCenSave.IsEnabled = true;
            btnCenSave.Background = (Brush)new BrushConverter().ConvertFrom("#7ED957")!;
        }

        // --- NÚT SAVE: LƯU XUỐNG SQL VÀ KHÓA LẠI ---
        private void btnCenSave_Click(object sender, RoutedEventArgs e)
        {
            // 1. Kiểm tra an toàn: Đảm bảo người dùng nhập đúng số nguyên
            if (!int.TryParse(txtCenVel.Text, out int velocity) || !int.TryParse(txtCenTime.Text, out int spinTime))
            {
                MessageBox.Show("Velocity and Time must be valid integers!", "Input error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string connString = Properties.Settings.Default.SQL_String;
            if (string.IsNullOrEmpty(connString)) return;

            // 2. Lưu xuống Database SQL
            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    // Lệnh UPDATE vào hàng có SettingID = 1
                    string sqlUpdateCen = "UPDATE tb_Centrifuge SET Velocity = @v, SpinTime = @t WHERE SettingID = 1";

                    using (SqlCommand cmd = new SqlCommand(sqlUpdateCen, conn))
                    {
                        cmd.Parameters.AddWithValue("@v", velocity);
                        cmd.Parameters.AddWithValue("@t", spinTime);
                        cmd.ExecuteNonQuery(); // Thực thi lưu
                    }
                }
                MessageBox.Show("Centrifuge parameters have been successfully updated!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // 3. Khóa giao diện lại như cũ sau khi lưu xong
                txtCenVel.IsReadOnly = true;
                txtCenTime.IsReadOnly = true;
                txtCenVel.Background = (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
                txtCenTime.Background = (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;

                btnCenEdit.IsEnabled = true;
                btnCenEdit.Background = (Brush)new BrushConverter().ConvertFrom("#0054A6")!;

                btnCenSave.IsEnabled = false;
                btnCenSave.Background = (Brush)new BrushConverter().ConvertFrom("#A6A6A6")!;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu thông số Ly tâm:\n" + ex.Message, "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 3. Hàm hiển thị lên textbox frm teaching

        public void fn_Tags_Teaching_To_Textbox()
        {
            // KHÔNG CẦN DÙNG Dispatcher Ở ĐÂY NỮA VÌ MAINWINDOW ĐÃ BỌC LUỒNG UI BẰNG BeginInvoke RỒI
            txtPosX.Text = Class_Common.PosTrucX.ToString("F2");
            txtPosY.Text = Class_Common.PosTrucY.ToString("F2");
            txtPosZ.Text = Class_Common.PosTrucZ.ToString("F2");
            txtPosR.Text = Class_Common.PosTrucR.ToString("F2");
            txtCenCurrent.Text = Class_Common.PosServo.ToString("F2");
        }
        #endregion
    }
}