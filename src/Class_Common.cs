using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SCADA_VERTEX
{
    class Class_Common
    {
        #region 1. Hàm xác nhận thao tác
        // 1. Hàm xác nhận thao tác, trả về true nếu người dùng chọn "Yes", false nếu chọn "No"
        public static bool fn_Confirm()
        {
            string message = "Do you want to continue ?";
            string title = "Confirm the operation";

            MessageBoxResult result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
        #endregion
        #region 2. TIMER QUÉT TAG (ĐÃ TỐI ƯU LUỒNG NỀN)

        // DÙNG System.Timers.Timer ĐỂ CHẠY DƯỚI BACKGROUND, KHÔNG LÀM TREO UI
        private static System.Timers.Timer? timerPLCTag;
        public static event Action? OnPLCScan;

        public static void Timer_PLCTagscan(int scan_time)
        {
            try
            {
                if (scan_time <= 0)
                {
                    throw new ArgumentException("Invalid. It must be a positive number!");
                }

                if (timerPLCTag == null)
                {
                    timerPLCTag = new System.Timers.Timer();

                    // Sự kiện Elapsed của System.Timers.Timer luôn tự động chạy trên luồng nền
                    timerPLCTag.Elapsed += (sender, args) =>
                    {
                        try
                        {
                            // Tạm dừng timer trong lúc đọc để tránh nghẽn luồng nếu đọc quá lâu
                            timerPLCTag.Stop();

                            OnPLCScan?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing PLC data: {ex.Message}");
                        }
                        finally
                        {
                            timerPLCTag.Start(); // Đọc xong thì cho chạy lại
                        }
                    };
                }

                timerPLCTag.Interval = scan_time;
                timerPLCTag.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when initializing timer: {ex.Message}");
            }
        }
        #endregion
        #region 3. HÀM KIỂM TRA KẾT NỐI WATCHDOG

        // Định nghĩa các biến bên ngoài phương thức
        static int watchdog_last_val = 0;
        static DateTime last_watchdog_time = DateTime.Now;

        // Hàm kiểm tra trạng thái kết nối
        public static bool CheckConnectionStatusPLC(string tag_watchdog_in)
        {
            try
            {
                // Kiểm tra và chuyển đổi tag_watchdog_in thành int
                if (!int.TryParse(tag_watchdog_in, out int tag_watchdog))
                {
                    // Nếu không thể chuyển đổi, trả về false (không có kết nối)
                    return false;
                }

                // Kiểm tra xem tag_watchdog có thay đổi hay không
                if (tag_watchdog != watchdog_last_val)
                {
                    // Nếu có thay đổi, reset thời gian và cập nhật lại giá trị của tag_watchdog
                    watchdog_last_val = tag_watchdog;
                    last_watchdog_time = DateTime.Now; // Cập nhật thời gian mới
                    return true; // Trả về true vì có thay đổi trong vòng 5 giây
                }
                else
                {
                    // Nếu không thay đổi, kiểm tra thời gian đã trôi qua kể từ lần kiểm tra trước
                    TimeSpan elapsed = DateTime.Now - last_watchdog_time;

                    if (elapsed.TotalSeconds >= 5)
                    {
                        // Nếu đã trôi qua 5 giây mà không có thay đổi, trả về false (mất kết nối)
                        return false;
                    }
                }

                // Nếu không có vấn đề gì và vẫn trong 5 giây, trả về true
                return true;
            }
            catch (Exception ex)
            {
                // Ghi log nếu có lỗi (có thể dùng phương thức ghi log của ứng dụng)
                Console.WriteLine("Error in CheckConnectionStatus: " + ex.Message);
                return false; // Trả về false nếu có lỗi
            }
        }
        #endregion
        #region 4. Các biến dùng chung
        public static double PosTrucX { get; set; } = 0.0;
        public static double PosTrucY { get; set; } = 0.0;
        public static double PosTrucZ { get; set; } = 0.0;
        public static double PosTrucR { get; set; } = 0.0;
        public static double PosServo { get; set; } = 0.0;
        public static int SpeedServo { get; set; } = 0;
        #endregion
    }
}
