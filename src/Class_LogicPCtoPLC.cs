using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SCADA_VERTEX
{
    internal class Class_LogicPCtoPLC
    {
        //================================================================ Các biến bước =======================================================
        public static bool M0 { get; set; } = false;
        public static bool M1 { get; set; } = false;
        public static bool M2 { get; set; } = false;
        public static bool M3 { get; set; } = false;
        public static bool M4 { get; set; } = false;
        public static bool M5 { get; set; } = false;
        public static bool M6 { get; set; } = false;
        public static bool M7 { get; set; } = false;
        public static bool M8 { get; set; } = false;
        public static bool M9 { get; set; } = false;
        public static bool M10 { get; set; } = false;
        public static bool M11 { get; set; } = false;
        public static bool M12 { get; set; } = false;
        public static bool M13 { get; set; } = false;
        public static bool M14 { get; set; } = false;
        public static bool M15 { get; set; } = false;
        public static bool M16 { get; set; } = false;
        public static bool M17 { get; set; } = false;
        public static bool M18 { get; set; } = false;
        public static bool M19 { get; set; } = false;
        public static bool M20 { get; set; } = false;
        public static bool M21 { get; set; } = false;
        public static bool M22 { get; set; } = false;
        public static bool M23 { get; set; } = false;
        public static bool M24 { get; set; } = false;
        public static bool M25 { get; set; } = false;
        public static bool M26 { get; set; } = false;
        public static bool M27 { get; set; } = false;
        public static bool M28 { get; set; } = false;
        public static bool M91 { get; set; } = false;

        //========================================================= Các miền nhớ và bit bên PLC =========================================================================
        public static int Status_processing { get; set; } = 0;
        public static int Quantity_TubeMain { get; set; } = 0;
        public static int Quantity_TubeSub { get; set; } = 0;
        public static bool Current_M103_Status = false;
        public static bool Done_In_Out_Grip { get; set; } = false;
        public static bool Dang_Ly_tam { get; set; } = false;
        public static bool Tin_hieu_allow_khay { get; set; } = false; // M302

        // ==========================================================
        // CỜ KHÓA AN TOÀN CHỐNG NHIỄU & BẮT TAY NỘI SUY
        // ==========================================================
        public static bool IsGripperHoldingTube = false;
        public static bool Signal_SpeedCalculated = false; // [MỚI]: Cờ theo dõi trạng thái bắt tay vận tốc (Giống Signal_UpdatedOffset)

        //======================================================== Digital Twin (Khay ảo) ===============================================================================
        public static List<string> SimulatedBarcodes = new List<string> {
            "97531874", "97531878", "97531868", "97531860", "97531862", "97531864",
            "12345683", "12345682", "12345681", "12345680", "12345679", "12345678"
        };
        public static Random rand = new Random();

        // [MỚI]: Bộ đệm chứa các barcode chưa được đọc (Dùng để random KHÔNG LẶP LẠI)
        private static List<string> _availableBarcodes = new List<string>(SimulatedBarcodes);

        public static int[] BatchIDs = new int[4];
        public static string[] BatchStatus = new string[4] { "", "", "", "" };
        public static int CurrentProcessingSlot = -1;

        // ==========================================================
        // KHAI BÁO BỘ ĐẾM SỐ LẦN CHẠY CỦA TỪNG KHAY (Lần 1, Lần 2...)
        // ==========================================================
        public static int BatchCount_Tray1 = 1;
        public static int BatchCount_Tray2 = 1;
        public static int BatchCount_Tray3 = 1;
        public static int BatchCount_TrayErr = 1;

        public struct TubeData
        {
            public int ID;
            public short TypeTest;
            public bool Need_Centrifuge;

            // Gắn mốc thời gian, Tên mẻ và chuỗi kết quả vào riêng từng ống
            public DateTime StartTime;
            public string BatchName;
            public string ResultText;
        }

        public static TubeData CurrentHeldTube = new TubeData();

        public static TubeData[] VirtualTray_TG = new TubeData[9];
        public static Queue<int> Queue_TG_Order = new Queue<int>();

        public static TubeData[] Virtual_Cen = new TubeData[4];
        public static int Index_LT_Place = 0;
        public static int Index_LT_Pick = 0;

        // --- KHAY ỐNG BÙ ---
        public static TubeData[] VirtualTray_OngBu = new TubeData[3] {
            new TubeData { ID = 9999, TypeTest = 0, Need_Centrifuge = true },
            new TubeData { ID = 9999, TypeTest = 0, Need_Centrifuge = true },
            new TubeData { ID = 9999, TypeTest = 0, Need_Centrifuge = true }
        };

        // ==========================================================
        // BIẾN QUẢN LÝ ỐNG BÙ (ROUND-ROBIN & QUEUE)
        // ==========================================================
        public static int Next_Pick_OngBu_Index = 0; // Trỏ tới lỗ sẽ gắp tiếp theo
        public static Queue<int> Missing_OngBu_Slots = new Queue<int>(); // Ghi nhớ các lỗ đang bị trống để trả về

        //======================================================== Index các khay =======================================================================================
        public static int Index_Tray1 = 0;
        public static int Index_Tray2 = 0;
        public static int Index_Tray3 = 0;
        public static int Index_TrayErr = 0;

        public static int Index_TrayTG_Place = 0;
        public static int Index_TrayTG_Pick = 0;

        public static short Total_Err = 0;
        public static short Total_OK = 0;

        public static bool IsTray1_Full = false;
        public static bool IsTray2_Full = false;
        public static bool IsTray3_Full = false;
        public static bool IsTrayErr_Full = false;
        public static bool IsGlobal_LayKhay = false;

        public static bool Signal_UpdatedOffset = false;
        public static bool Signal_DoneVision = false;
        public static short State = 0;
        // THÊM BIẾN NÀY ĐỂ NHỚ TRẠNG THÁI TRƯỚC ĐÓ
        private static short _lastState = -1;

        //======================================================== HÀM TÍNH TOÁN OFFSET =================================================================================
        public static void fn_UpdateNextOffsetToPLC(string trayPrefix, int nextIndex)
        {
            try
            {
                int cols = 2; int rows = 4;

                if (trayPrefix == "TrayTG") { cols = 3; rows = 3; }
                else if (trayPrefix == "TrayOngBu") { cols = 3; rows = 1; }
                if (nextIndex >= (cols * rows)) return;

                frm_teaching.RobotPoint? p1_Up = null;
                frm_teaching.RobotPoint? p1_Down = null;
                frm_teaching.RobotPoint? p2_Down = null;
                frm_teaching.RobotPoint? p3_Down = null;

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (frm_teaching.Data_transfer != null && frm_teaching.Data_transfer.RobotPoints != null)
                    {
                        var points = frm_teaching.Data_transfer.RobotPoints;
                        p1_Up = points.FirstOrDefault(p => p.PointName == $"{trayPrefix}_Up");
                        p1_Down = points.FirstOrDefault(p => p.PointName == $"{trayPrefix}_Down");
                        p2_Down = points.FirstOrDefault(p => p.PointName == $"{trayPrefix}_P2");
                        p3_Down = points.FirstOrDefault(p => p.PointName == $"{trayPrefix}_P3");
                    }
                });

                if (p1_Up == null || p1_Down == null || p2_Down == null || p3_Down == null) return;

                System.Globalization.CultureInfo cul = System.Globalization.CultureInfo.InvariantCulture;

                double baseZ_Up = double.Parse(p1_Up.Z, cul);
                double baseZ_Down = double.Parse(p1_Down.Z, cul);
                double z_Stroke = baseZ_Up - baseZ_Down;

                int col = nextIndex % cols;
                int row = nextIndex / cols;
                int divX = Math.Max(1, cols - 1);
                int divY = Math.Max(1, rows - 1);

                double stepCol_X = (double.Parse(p2_Down.X, cul) - double.Parse(p1_Down.X, cul)) / divX;
                double stepCol_Y = (double.Parse(p2_Down.Y, cul) - double.Parse(p1_Down.Y, cul)) / divX;
                double stepCol_Z = (double.Parse(p2_Down.Z, cul) - double.Parse(p1_Down.Z, cul)) / divX;

                double stepRow_X = (double.Parse(p3_Down.X, cul) - double.Parse(p1_Down.X, cul)) / divY;
                double stepRow_Y = (double.Parse(p3_Down.Y, cul) - double.Parse(p1_Down.Y, cul)) / divY;
                double stepRow_Z = (double.Parse(p3_Down.Z, cul) - double.Parse(p1_Down.Z, cul)) / divY;

                // 1. TỌA ĐỘ ĐÍCH (Đơn vị: mm)
                double targetX = double.Parse(p1_Down.X, cul) + (col * stepCol_X) + (row * stepRow_X);
                double targetY = double.Parse(p1_Down.Y, cul) + (col * stepCol_Y) + (row * stepRow_Y);
                double targetZ_Down = baseZ_Down + (col * stepCol_Z) + (row * stepRow_Z);
                double targetZ_Up = targetZ_Down + z_Stroke;

                // 2. CHUYỂN ĐỔI SANG XUNG
                double k_Linear = 800.0 / 8.0; // 100 xung/mm
                int pulse_X = (int)Math.Round(targetX * k_Linear);
                int pulse_Y = (int)Math.Round(targetY * k_Linear);
                int pulse_Z_Up = (int)Math.Round(targetZ_Up * k_Linear);
                int pulse_Z_Down = (int)Math.Round(targetZ_Down * k_Linear);

                if (MainWindow.Data_transfer != null)
                {
                    // GỬI TỌA ĐỘ VỊ TRÍ XUỐNG PLC
                    MainWindow.Data_transfer.TagWrite($"Tag_{trayPrefix}X", pulse_X);
                    MainWindow.Data_transfer.TagWrite($"Tag_{trayPrefix}Y", pulse_Y);
                    MainWindow.Data_transfer.TagWrite($"Tag_{trayPrefix}Z", pulse_Z_Up);
                    MainWindow.Data_transfer.TagWrite($"Tag_{trayPrefix}DownZ", pulse_Z_Down);
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI TÍNH OFFSET]: {ex.Message}");
            }
        }


        // =========================================================================================================
        // [MỚI]: HÀM BẮT TAY NỘI SUY GỌI TRỰC TIẾP TRONG TỪNG CASE (M108 -> M109)
        // =========================================================================================================
        private static int _isCalculatingSpeed = 0; // Khóa luồng CPU chống gọi đè
        public static bool isM108_Requested = false;

        public static void fn_HandleSpeedHandshake()
        {
            if (MainWindow.Data_transfer == null) return;

            // Đọc cờ yêu cầu tính tốc độ từ PLC (M108)
            bool isM108_Requested_Local = MainWindow.Data_transfer.TagRead<bool>("M108");

            // --- BƯỚC 1: PLC YÊU CẦU TÍNH TOÁN (M108 = ON) VÀ PC CHƯA PHẢN HỒI ---
            if (isM108_Requested_Local == true && Signal_SpeedCalculated == false)
            {
                if (Interlocked.CompareExchange(ref _isCalculatingSpeed, 1, 0) != 0) return;

                try
                {
                    Signal_SpeedCalculated = true; // Khóa cờ ngay lập tức để không bị tính lặp lại

                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(20); // Đợi 20ms để Kepware kịp cập nhật D470, D472 mới nhất từ PLC lên RAM

                            // Đọc tọa độ hiện tại và đích từ PLC
                            int currentX = MainWindow.Data_transfer.TagRead<int>("Tag_PosX");
                            int currentY = MainWindow.Data_transfer.TagRead<int>("Tag_PosY");
                            int targetX = MainWindow.Data_transfer.TagRead<int>("D94");
                            int targetY = MainWindow.Data_transfer.TagRead<int>("D96");

                            // Tính toán tam giác đồng dạng nội suy 2 trục XY
                            double deltaX = Math.Abs(targetX - currentX);
                            double deltaY = Math.Abs(targetY - currentY);
                            double totalDist = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                            int baseSpeed = 20000; // Tốc độ tổng 20 kHz (~200 mm/s)
                            int speedX = baseSpeed;
                            int speedY = baseSpeed;

                            if (totalDist > 0)
                            {
                                speedX = (int)Math.Round(baseSpeed * (deltaX / totalDist));
                                speedY = (int)Math.Round(baseSpeed * (deltaY / totalDist));
                                speedX = Math.Max(200, speedX); // Khóa tốc độ sàn bảo vệ motor
                                speedY = Math.Max(200, speedY);
                            }

                            // Ghi tốc độ xuống PLC
                            MainWindow.Data_transfer.TagWrite("Tag_CamVelX", speedX);
                            MainWindow.Data_transfer.TagWrite("Tag_CamVelY", speedY);

                            await Task.Delay(150); // Đợi Kepware nhồi bộ nhớ D xuống PLC

                            // PC TÍNH XONG -> PHẢN HỒI M109 = ON ĐỂ PLC CHẠY
                            MainWindow.Data_transfer.TagWrite("M111", true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Lỗi Handshake nội suy: " + ex.Message);
                            Signal_SpeedCalculated = false; // Nhả cờ để cho phép thử lại nếu mạng bị lỗi
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _isCalculatingSpeed, 0);
                        }
                    });
                }
                catch
                {
                    Interlocked.Exchange(ref _isCalculatingSpeed, 0);
                }
            }
            // --- BƯỚC 2: PLC ĐÃ NHẬN TỐC ĐỘ, CHẠY MOTOR VÀ TỰ TẮT M108 = OFF ---
            else if (isM108_Requested_Local == false && Signal_SpeedCalculated == true)
            {
                // C# nhận thấy PLC nhả M108 thì C# cũng tự thu hồi M109 và dọn dẹp cờ
                Signal_SpeedCalculated = false;
                MainWindow.Data_transfer.TagWrite("M111", false);
            }
        }


        public static void fn_Check_TrayTG_Empty()
        {
            bool hasTubes = (Queue_TG_Order.Count > 0);

            if (hasTubes == true && Current_M103_Status == false)
            {
                Current_M103_Status = true;
                MainWindow.Data_transfer!.TagWrite("M103", true);
            }
            else if (hasTubes == false && Current_M103_Status == true)
            {
                Current_M103_Status = false;
                MainWindow.Data_transfer!.TagWrite("M103", false);
            }
        }

        // =========================================================================================================
        // TỐI ƯU HÓA LUỒNG 1: BỘ NHỚ ĐỆM CHUỖI TRẠNG THÁI (Tránh spam giao diện HMI/SCADA)
        // =========================================================================================================
        private static string _lastStatusText = "";
        private static void UpdateMainStatus(string statusText)
        {
            if (_lastStatusText == statusText) return;
            _lastStatusText = statusText;
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                if (frm_main.Data_transfer != null)
                    frm_main.Data_transfer.txt_trangthai.Text = statusText;
            }));
        }

        // =========================================================================================================
        // TỐI ƯU HÓA LUỒNG 2: HÀM CẬP NHẬT HIS BẤT ĐỒNG BỘ (Chạy ngầm 100%, không khóa luồng PLC)
        // =========================================================================================================
        private static void UpdateHisResultAsync(string sampleId, string resultStr)
        {
            Task.Run(() =>
            {
                try
                {
                    string connStr = Properties.Settings.Default.SQL_String;
                    if (string.IsNullOrEmpty(connStr)) return;
                    using (SqlConnection conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        string sqlUpdate = "UPDATE Tbl_PatientProfile SET Result = @res WHERE SampleID = @id";
                        using (SqlCommand cmd = new SqlCommand(sqlUpdate, conn))
                        {
                            cmd.Parameters.AddWithValue("@res", resultStr);
                            cmd.Parameters.AddWithValue("@id", sampleId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine("Lỗi SQL HIS Update: " + ex.Message); }
            });
        }

        // =========================================================
        // HÀM INSERT DATALOG CẬP NHẬT ĐỦ 7 TRƯỜNG DỮ LIỆU & TỐI ƯU LUỒNG NGẦM
        // =========================================================
        public static void InsertDataLog(string barcode, short typeTest, string result, string batchName, DateTime startTime, double durationSec)
        {
            Task.Run(() =>
            {
                try
                {
                    string connStr = Properties.Settings.Default.SQL_String;
                    if (string.IsNullOrEmpty(connStr)) return;

                    using (SqlConnection conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        string typeName = typeTest == 1 ? "Hematology" :
                                          typeTest == 2 ? "Chemistry" :
                                          typeTest == 3 ? "Immunity" : "Unknown";

                        string query = @"INSERT INTO Tbl_DataLog 
                                        (TimeScan, Barcode, TestType, Result, BatchName, StartTime, DurationSec) 
                                        VALUES (@timeScan, @barcode, @type, @res, @batch, @start, @dur)";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@timeScan", DateTime.Now);
                            cmd.Parameters.AddWithValue("@barcode", string.IsNullOrEmpty(barcode) ? "NO_BARCODE" : barcode);
                            cmd.Parameters.AddWithValue("@type", typeName);
                            cmd.Parameters.AddWithValue("@res", result);
                            cmd.Parameters.AddWithValue("@batch", string.IsNullOrEmpty(batchName) ? "Unknown Batch" : batchName);
                            cmd.Parameters.AddWithValue("@start", startTime == DateTime.MinValue ? DateTime.Now : startTime);
                            cmd.Parameters.AddWithValue("@dur", Math.Round(durationSec, 2));

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi ghi DataLog: " + ex.Message);
                }
            });
        }
        // [MỚI]: Hàm lấy Barcode ngẫu nhiên KHÔNG LẶP LẠI cho đến khi hết danh sách
        public static string GetNextRandomBarcode()
        {
            // Nếu đã chạy gắp hết sạch 12 ống nghiệm thì tự động Reset lại bộ đệm cho vòng chạy mới
            if (_availableBarcodes.Count == 0)
            {
                _availableBarcodes = new List<string>(SimulatedBarcodes);
                Debug.WriteLine("[DIGITAL TWIN] Đã đọc hết 12 mã. Làm mới danh sách Barcode!");
            }

            // Chọn ngẫu nhiên 1 chỉ số từ danh sách những mã CHƯA ĐỌC
            int randomIndex = rand.Next(_availableBarcodes.Count);
            string selectedBarcode = _availableBarcodes[randomIndex];

            // Xóa mã vừa chọn khỏi bộ đệm để lần đọc tiếp theo chắc chắn không bao giờ bị trùng
            _availableBarcodes.RemoveAt(randomIndex);

            return selectedBarcode;
        }

        public static void fn_type_offset_2()
        {
            if (M0) { State = 0; }
            else if (M1) { State = 1; }
            else if (M2) { State = 2; }
            else if (M3) { State = 3; }
            else if (M4) { State = 4; }
            else if (M5) { State = 5; }
            else if (M6) { State = 6; }
            else if (M7) { State = 7; }
            else if (M8) { State = 8; }
            else if (M9) { State = 9; }
            else if (M10) { State = 10; }
            else if (M11) { State = 11; }
            else if (M12) { State = 12; }
            else if (M13) { State = 13; }
            else if (M14) { State = 14; }
            else if (M15) { State = 15; }
            else if (M16) { State = 16; }
            else if (M17) { State = 17; }
            else if (M18) { State = 18; }
            else if (M19) { State = 19; }
            else if (M20) { State = 20; }
            else if (M21) { State = 21; }
            else if (M22) { State = 22; }
            else if (M23) { State = 23; }
            else if (M24) { State = 24; }
            else if (M25) { State = 25; }
            else if (M26) { State = 26; }
            else if (M27) { State = 27; }
            else if (M28) { State = 28; }
            else if (M91) { State = 91; }

            if (State != 11 && State != 26)
            {
                Signal_DoneVision = false;
            }

            switch (State)
            {
                case 0:
                    break;

                case 1:
                    //UpdateMainStatus("HOME...");
                    break;

                case 2:
                    UpdateMainStatus("MOVE TO BELT");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        CurrentHeldTube.StartTime = DateTime.Now;

                        Task.Run(async () =>
                        {
                            await Task.Delay(10);
                            MainWindow.Data_transfer!.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 26:
                    if (State != _lastState)
                    {
                        _lastState = State;
                        Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            frm_setting.Data_transfer?.ChangeCameraExposureAuto(2500);
                            frm_setting.Data_transfer?.ClearTriggeredImages();
                        }));
                    }

                    UpdateMainStatus("BARCODE READING");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;

                        if (IsGripperHoldingTube == false)
                        {
                            IsGripperHoldingTube = true;
                            //string? ok = frm_setting.Data_transfer!.barcode_reading_auto();
                            string? barcodeText = GetNextRandomBarcode();
                            //string? barcodeText = "12345678";
                            short testType = 4;
                            bool needLT = false;
                            bool isError = true;

                            if (!string.IsNullOrEmpty(barcodeText))
                            {
                                if (int.TryParse(barcodeText, out int parsedID)) CurrentHeldTube.ID = parsedID;

                                try
                                {
                                    string connStr = Properties.Settings.Default.SQL_String;
                                    using (SqlConnection conn = new SqlConnection(connStr))
                                    {
                                        conn.Open();
                                        string query = "SELECT TestType, CentrifugeNeeded FROM Tbl_PatientProfile WHERE SampleID = @id";
                                        using (SqlCommand cmd = new SqlCommand(query, conn))
                                        {
                                            cmd.Parameters.AddWithValue("@id", barcodeText);
                                            using (SqlDataReader reader = cmd.ExecuteReader())
                                            {
                                                if (reader.Read())
                                                {
                                                    isError = false;
                                                    string tType = reader["TestType"].ToString() ?? "";
                                                    if (tType.Equals("Hematology", StringComparison.OrdinalIgnoreCase)) testType = 1;
                                                    else if (tType.Equals("Chemistry", StringComparison.OrdinalIgnoreCase)) testType = 2;
                                                    else if (tType.Equals("Immunity", StringComparison.OrdinalIgnoreCase)) testType = 3;
                                                    else testType = 4;

                                                    string cenStr = reader["CentrifugeNeeded"].ToString() ?? "";
                                                    needLT = cenStr.Equals("Yes", StringComparison.OrdinalIgnoreCase);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex) { Console.WriteLine("Lỗi SQL HIS: " + ex.Message); }
                            }

                            CurrentHeldTube.TypeTest = testType;
                            CurrentHeldTube.Need_Centrifuge = needLT;

                            if (isError)
                            {
                                CurrentHeldTube.BatchName = $"Error Tray Batch - Run {BatchCount_TrayErr}";
                                CurrentHeldTube.ResultText = "Error: Barcode/Database Not Found";
                            }
                            else if (testType == 1)
                            {
                                CurrentHeldTube.BatchName = $"Tray 1 Batch - Run {BatchCount_Tray1}";
                                CurrentHeldTube.ResultText = "OK (No Centrifuge)";
                            }
                            else if (testType == 2)
                            {
                                CurrentHeldTube.BatchName = $"Tray 2 Batch - Run {BatchCount_Tray2}";
                            }
                            else if (testType == 3)
                            {
                                CurrentHeldTube.BatchName = $"Tray 3 Batch - Run {BatchCount_Tray3}";
                            }
                            else
                            {
                                CurrentHeldTube.BatchName = $"Tray 1 Batch - Run {BatchCount_Tray1}";
                            }

                            if (!isError && needLT)
                            {
                                if (!(Dang_Ly_tam == false && Quantity_TubeMain < 4))
                                {
                                    int nextSlot = -1;
                                    for (int i = 0; i < VirtualTray_TG.Length; i++)
                                    {
                                        if (VirtualTray_TG[i].ID == 0) { nextSlot = i; break; }
                                    }
                                    if (nextSlot != -1)
                                    {
                                        Index_TrayTG_Place = nextSlot;
                                        fn_UpdateNextOffsetToPLC("TrayTG", Index_TrayTG_Place);
                                    }
                                }
                            }

                            Task.Run(async () =>
                            {
                                MainWindow.Data_transfer!.TagWrite("M203", isError);
                                MainWindow.Data_transfer.TagWrite("M101", needLT);
                                await Task.Delay(1);
                                MainWindow.Data_transfer.TagWrite("M304", true);
                            });
                        }
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 11:
                    if (State != _lastState)
                    {
                        _lastState = State;
                        Application.Current?.Dispatcher?.BeginInvoke(new Action(() => {
                            frm_setting.Data_transfer?.ChangeCameraExposureAuto(1000.0);
                            frm_setting.Data_transfer?.ClearTriggeredImages();
                        }));
                    }
                    UpdateMainStatus("LEVEL CHECKING");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        double? rbc = 2;
                        double? plasma = 3;
                        string failReason = "OK";
                        bool isLevelError = false;

                        if (rbc == null || plasma == null || failReason != "OK") isLevelError = true;

                        if (isLevelError == true) BatchStatus[CurrentProcessingSlot] = "ERROR ...";
                        else BatchStatus[CurrentProcessingSlot] = "DECAPPING ...";

                        frm_main.Data_transfer?.fn_UpdateLiquidLevel_UI(plasma, rbc, isLevelError ? "ERROR" : "OK");

                        string finalResultString = "";
                        if (!isLevelError && CurrentHeldTube.ID != 0)
                        {
                            finalResultString = $"OK (RBC: {rbc:F1}mm, Plasma: {plasma:F1}mm)";
                            UpdateHisResultAsync(CurrentHeldTube.ID.ToString(), finalResultString);
                        }
                        else
                        {
                            finalResultString = $"Error: {failReason}";
                        }

                        CurrentHeldTube.ResultText = finalResultString;
                        if (isLevelError) CurrentHeldTube.BatchName = $"Error Tray Batch - Run {BatchCount_TrayErr}";

                        Task.Run(async () =>
                        {
                            MainWindow.Data_transfer!.TagWrite("M203", isLevelError);
                            MainWindow.Data_transfer.TagWrite("D2", isLevelError ? 4 : CurrentHeldTube.TypeTest);
                            await Task.Delay(150);
                            MainWindow.Data_transfer.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 3: // Gắp vô ly tâm (THẢ ỐNG CHÍNH)
                    UpdateMainStatus("PLACE INTO CENTRIFUGE");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == true)
                        {
                            if (Index_LT_Place == 0)
                            {
                                for (int i = 0; i < 4; i++) { BatchIDs[i] = 0; BatchStatus[i] = ""; }
                            }
                            Virtual_Cen[Index_LT_Place] = CurrentHeldTube;
                            if (CurrentHeldTube.ID != 0)
                            {
                                BatchIDs[Index_LT_Place] = CurrentHeldTube.ID;
                                BatchStatus[Index_LT_Place] = "Waiting";
                            }
                            Index_LT_Place++;
                            if (Index_LT_Place > 3) Index_LT_Place = 0;
                            IsGripperHoldingTube = false;
                            CurrentHeldTube = new TubeData();
                        }
                        Task.Run(async () =>
                        {
                            await Task.Delay(150);
                            MainWindow.Data_transfer!.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 9: // Gắp ra ly tâm (RÚT ỐNG RA)
                    UpdateMainStatus("PICK FROM CENTRIFUGE");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == false)
                        {
                            IsGripperHoldingTube = true;
                            CurrentProcessingSlot = Index_LT_Pick;
                            if (CurrentProcessingSlot >= 0 && CurrentProcessingSlot < 4 && BatchIDs[CurrentProcessingSlot] != 0)
                            {
                                BatchStatus[CurrentProcessingSlot] = "Checking...";
                            }
                            CurrentHeldTube = Virtual_Cen[Index_LT_Pick];
                            Virtual_Cen[Index_LT_Pick] = new TubeData();
                            Index_LT_Pick++;
                            if (Index_LT_Pick > 3) Index_LT_Pick = 0;
                            if (CurrentHeldTube.ID == 9999 && Missing_OngBu_Slots.Count > 0)
                            {
                                int returnSlot = Missing_OngBu_Slots.Peek();
                                fn_UpdateNextOffsetToPLC("TrayOngBu", returnSlot);
                            }
                        }
                        Task.Run(async () =>
                        {
                            await Task.Delay(150);
                            MainWindow.Data_transfer!.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 6: // Gắp vào trung gian
                    UpdateMainStatus("PLACE INTO TG");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        bool isTGFull = false;
                        if (IsGripperHoldingTube == true)
                        {
                            VirtualTray_TG[Index_TrayTG_Place] = CurrentHeldTube;
                            Queue_TG_Order.Enqueue(Index_TrayTG_Place);
                            if (Queue_TG_Order.Count > 0)
                            {
                                Index_TrayTG_Pick = Queue_TG_Order.Peek();
                                fn_UpdateNextOffsetToPLC("TrayTG", Index_TrayTG_Pick);
                            }
                            fn_Check_TrayTG_Empty();

                            if (Queue_TG_Order.Count >= 9)
                            {
                                isTGFull = true;
                            }

                            IsGripperHoldingTube = false;
                            CurrentHeldTube = new TubeData();
                        }
                        Task.Run(async () =>
                        {
                            MainWindow.Data_transfer!.TagWrite("D50", Queue_TG_Order.Count);
                            if (isTGFull) MainWindow.Data_transfer.TagWrite("M205", true);
                            await Task.Delay(150);
                            MainWindow.Data_transfer.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 24: // Gắp ra trung gian 
                    UpdateMainStatus("PICK FROM TG");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == false)
                        {
                            IsGripperHoldingTube = true;
                            CurrentHeldTube = VirtualTray_TG[Index_TrayTG_Pick];
                            VirtualTray_TG[Index_TrayTG_Pick] = new TubeData();
                            if (Queue_TG_Order.Count > 0) Queue_TG_Order.Dequeue();
                            if (Queue_TG_Order.Count > 0)
                            {
                                Index_TrayTG_Pick = Queue_TG_Order.Peek();
                                fn_UpdateNextOffsetToPLC("TrayTG", Index_TrayTG_Pick);
                            }
                            fn_Check_TrayTG_Empty();
                        }
                        Task.Run(async () =>
                        {
                            MainWindow.Data_transfer!.TagWrite("D50", Queue_TG_Order.Count);
                            MainWindow.Data_transfer.TagWrite("M205", false);
                            await Task.Delay(150);
                            MainWindow.Data_transfer.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 8: // Khay 1
                    UpdateMainStatus("MOVE TO TRAY1");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == true)
                        {
                            Total_OK++;
                            if (CurrentProcessingSlot != -1)
                            {
                                BatchStatus[CurrentProcessingSlot] = "DONE";
                                CurrentProcessingSlot = -1;
                            }

                            string resultStr = "OK (No Centrifuge)";
                            UpdateHisResultAsync(CurrentHeldTube.ID.ToString(), resultStr);

                            double duration = (DateTime.Now - CurrentHeldTube.StartTime).TotalSeconds;
                            InsertDataLog(CurrentHeldTube.ID.ToString(), CurrentHeldTube.TypeTest, resultStr,
                                          CurrentHeldTube.BatchName, CurrentHeldTube.StartTime, duration);

                            Index_Tray1++;
                            IsGripperHoldingTube = false;
                            CurrentHeldTube = new TubeData();

                            Task.Run(async () =>
                            {
                                MainWindow.Data_transfer!.TagWrite("D51", Index_Tray1);
                                if (Index_Tray1 >= 8)
                                {
                                    Index_Tray1 = 8; IsTray1_Full = true;
                                    MainWindow.Data_transfer.TagWrite("M201", true);
                                }
                                else fn_UpdateNextOffsetToPLC("Tray1", Index_Tray1);
                                await Task.Delay(150);
                                MainWindow.Data_transfer.TagWrite("M304", true);
                            });
                        }
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 27: // Khay 2/3
                    UpdateMainStatus("MOVE TO TRAY 2/3");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == true)
                        {
                            Total_OK++;
                            if (CurrentProcessingSlot != -1)
                            {
                                BatchStatus[CurrentProcessingSlot] = "DONE";
                                CurrentProcessingSlot = -1;
                            }

                            string resultStr = CurrentHeldTube.ResultText ?? "OK (No Centrifuge)";
                            if (CurrentHeldTube.Need_Centrifuge == false)
                            {
                                resultStr = "OK (No Centrifuge)";
                                UpdateHisResultAsync(CurrentHeldTube.ID.ToString(), resultStr);
                            }

                            double duration = (DateTime.Now - CurrentHeldTube.StartTime).TotalSeconds;
                            InsertDataLog(CurrentHeldTube.ID.ToString(), CurrentHeldTube.TypeTest, resultStr,
                                          CurrentHeldTube.BatchName, CurrentHeldTube.StartTime, duration);

                            IsGripperHoldingTube = false;
                            TubeData savedTube = CurrentHeldTube;
                            CurrentHeldTube = new TubeData();

                            Task.Run(async () =>
                            {
                                if (savedTube.TypeTest == 2)
                                {
                                    Index_Tray2++;
                                    MainWindow.Data_transfer!.TagWrite("D52", Index_Tray2);
                                    if (Index_Tray2 >= 8)
                                    {
                                        Index_Tray2 = 8; IsTray2_Full = true;
                                        MainWindow.Data_transfer.TagWrite("M201", true);
                                    }
                                    else fn_UpdateNextOffsetToPLC("Tray2", Index_Tray2);
                                }
                                else
                                {
                                    Index_Tray3++;
                                    MainWindow.Data_transfer!.TagWrite("D53", Index_Tray3);
                                    if (Index_Tray3 >= 8)
                                    {
                                        Index_Tray3 = 8; IsTray3_Full = true;
                                        MainWindow.Data_transfer.TagWrite("M201", true);
                                    }
                                    else fn_UpdateNextOffsetToPLC("Tray3", Index_Tray3);
                                }
                                await Task.Delay(150);
                                MainWindow.Data_transfer.TagWrite("M304", true);
                            });
                        }
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 13: // Khay Lỗi (Từ Mực nước)
                    UpdateMainStatus("MOVE TO ERROR TRAY");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == true)
                        {
                            Total_Err++;
                            if (CurrentProcessingSlot != -1)
                            {
                                BatchStatus[CurrentProcessingSlot] = "ERROR";
                                CurrentProcessingSlot = -1;
                            }

                            double duration = (DateTime.Now - CurrentHeldTube.StartTime).TotalSeconds;
                            InsertDataLog(CurrentHeldTube.ID.ToString(), CurrentHeldTube.TypeTest,
                                          CurrentHeldTube.ResultText ?? "Error: Level Check Failed",
                                          CurrentHeldTube.BatchName, CurrentHeldTube.StartTime, duration);

                            Index_TrayErr++;
                            IsGripperHoldingTube = false;
                            CurrentHeldTube = new TubeData();

                            Task.Run(async () =>
                            {
                                MainWindow.Data_transfer!.TagWrite("D54", Queue_TG_Order.Count);
                                if (Index_TrayErr >= 8)
                                {
                                    Index_TrayErr = 8; IsTrayErr_Full = true;
                                    MainWindow.Data_transfer.TagWrite("M201", true);
                                }
                                else fn_UpdateNextOffsetToPLC("TrayErr", Index_TrayErr);
                                await Task.Delay(150);
                                MainWindow.Data_transfer.TagWrite("M304", true);
                            });
                        }
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 18: // Khay Lỗi Barcode
                    UpdateMainStatus("MOVE TO ERROR TRAY");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == true)
                        {
                            Total_Err++;
                            if (CurrentProcessingSlot != -1)
                            {
                                BatchStatus[CurrentProcessingSlot] = "ERROR";
                                CurrentProcessingSlot = -1;
                            }

                            double duration = (DateTime.Now - CurrentHeldTube.StartTime).TotalSeconds;
                            InsertDataLog(CurrentHeldTube.ID.ToString(), CurrentHeldTube.TypeTest,
                                          CurrentHeldTube.ResultText ?? "Error: Barcode/Database Not Found",
                                          CurrentHeldTube.BatchName, CurrentHeldTube.StartTime, duration);

                            Index_TrayErr++;
                            IsGripperHoldingTube = false;
                            CurrentHeldTube = new TubeData();

                            Task.Run(async () =>
                            {
                                MainWindow.Data_transfer!.TagWrite("D54", Total_Err);
                                if (Index_TrayErr >= 8)
                                {
                                    Index_TrayErr = 8; IsTrayErr_Full = true;
                                    MainWindow.Data_transfer.TagWrite("M201", true);
                                }
                                else fn_UpdateNextOffsetToPLC("TrayErr", Index_TrayErr);
                                await Task.Delay(150);
                                MainWindow.Data_transfer.TagWrite("M304", true);
                            });
                        }
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 22: // GẮP RA TỪ KHAY ỐNG BÙ (GẮP VÀO)
                    UpdateMainStatus("PICK ONG_BU TUBE FROM TRAY");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == false)
                        {
                            IsGripperHoldingTube = true;
                            int pickSlot = Next_Pick_OngBu_Index;
                            CurrentHeldTube = VirtualTray_OngBu[pickSlot];
                            VirtualTray_OngBu[pickSlot] = new TubeData();
                            Missing_OngBu_Slots.Enqueue(pickSlot);
                            for (int i = 0; i < 3; i++)
                            {
                                Next_Pick_OngBu_Index = (Next_Pick_OngBu_Index + 1) % 3;
                                if (VirtualTray_OngBu[Next_Pick_OngBu_Index].ID != 0) break;
                            }
                            fn_UpdateNextOffsetToPLC("TrayOngBu", Next_Pick_OngBu_Index);
                        }
                        Task.Run(async () =>
                        {
                            await Task.Delay(150);
                            MainWindow.Data_transfer!.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 28: // GẮP VÔ LY TÂM CHO ỐNG BÙ (THẢ ỐNG)
                    UpdateMainStatus("PLACE ONG_BU INTO CENTRIFUGE");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == true)
                        {
                            if (Index_LT_Place == 0)
                            {
                                for (int i = 0; i < 4; i++) { BatchIDs[i] = 0; BatchStatus[i] = ""; }
                            }
                            Virtual_Cen[Index_LT_Place] = CurrentHeldTube;
                            if (CurrentHeldTube.ID != 0)
                            {
                                BatchIDs[Index_LT_Place] = CurrentHeldTube.ID;
                                BatchStatus[Index_LT_Place] = "ONGBU";
                            }
                            Index_LT_Place++;
                            if (Index_LT_Place > 3) Index_LT_Place = 0;
                            IsGripperHoldingTube = false;
                            CurrentHeldTube = new TubeData();
                        }
                        Task.Run(async () =>
                        {
                            await Task.Delay(150);
                            MainWindow.Data_transfer!.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;

                case 16: // GẮP VỀ KHAY ỐNG BÙ (THẢ VỀ TRẢ LẠI)
                    UpdateMainStatus("PLACE ONG_BU TUBE TO TRAY");
                    fn_HandleSpeedHandshake();
                    if (Done_In_Out_Grip == true && Signal_UpdatedOffset == false)
                    {
                        Signal_UpdatedOffset = true;
                        if (IsGripperHoldingTube == true)
                        {
                            if (Missing_OngBu_Slots.Count > 0)
                            {
                                int returnedSlot = Missing_OngBu_Slots.Dequeue();
                                VirtualTray_OngBu[returnedSlot] = new TubeData { ID = 9999, TypeTest = 0, Need_Centrifuge = true };
                            }
                            IsGripperHoldingTube = false;
                            CurrentHeldTube = new TubeData();
                            fn_UpdateNextOffsetToPLC("TrayOngBu", Next_Pick_OngBu_Index);
                        }
                        Task.Run(async () =>
                        {
                            await Task.Delay(150);
                            MainWindow.Data_transfer!.TagWrite("M304", true);
                        });
                    }
                    else if (Done_In_Out_Grip == false && Signal_UpdatedOffset == true)
                    {
                        Signal_UpdatedOffset = false;
                        MainWindow.Data_transfer!.TagWrite("M304", false);
                    }
                    break;
            }
        }
    }
}