
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SCADA_VERTEX
{
    public partial class frm_main : UserControl
    {
        internal static frm_main? Data_transfer;

        public class CenTubeDisplay
        {
            public string TubeSlot { get; set; } = "";
            public string Barcode { get; set; } = "";
            public string Status { get; set; } = "";
        }

        public System.Collections.ObjectModel.ObservableCollection<CenTubeDisplay> CentrifugeList { get; set; } = new System.Collections.ObjectModel.ObservableCollection<CenTubeDisplay>();

        public bool _isAutoMode = false;
        public bool _State_lytam = false;
        private Storyboard? _centrifugeStoryboard;
        private bool _isCentrifugeAnimating = false;

        public bool isM2001_EMG = false;
        public bool isM2002_Alarm = false;
        public bool isM2003_Idle = false;
        public bool isM2004_Stop = false;
        public bool isM2005_TraysFull = false;

        private bool _hasOutTray1 = true;
        private bool _hasOutTray2 = true;
        private bool _hasOutTray3 = true;
        private bool _hasOutTrayErr = true;
        private bool _isWaitingForStart_To_SendM202 = false;
        public bool isSafeToTake = false;

        // ========================================================
        // TỐI ƯU HÓA: KHAI BÁO CÁC MÀU SẮC DẠNG TĨNH ĐỂ TRÁNH TRÀN RAM
        // ========================================================
        private static readonly SolidColorBrush BrushGreen = CreateFrozenBrush(Colors.Green);
        private static readonly SolidColorBrush BrushLime = CreateFrozenBrush(Colors.Lime);
        private static readonly SolidColorBrush BrushRed = CreateFrozenBrush(Colors.Red);
        private static readonly SolidColorBrush BrushGray = CreateFrozenBrush("#666666");
        private static readonly SolidColorBrush BrushDarkGray = CreateFrozenBrush("#444444");
        private static readonly SolidColorBrush BrushStopGray = CreateFrozenBrush("#A6A6A6");
        private static readonly SolidColorBrush BrushOrange = CreateFrozenBrush("#FF9933");
        private static readonly SolidColorBrush BrushBlue = CreateFrozenBrush("#00BFFF");
        private static readonly SolidColorBrush BrushLightGreen = CreateFrozenBrush("#D5F5E3");

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze(); // Đóng băng Brush để tăng hiệu năng Render
            return brush;
        }

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public frm_main()
        {
            InitializeComponent();
            Data_transfer = this;
            dgvCentrifugation.ItemsSource = CentrifugeList;

            // [MỚI]: Đọc số mẻ đã lưu từ ổ cứng lên khi vừa mở phần mềm
            LoadBatchCounters();

            Class_LogicPCtoPLC.fn_UpdateNextOffsetToPLC("TrayOngBu", 0);
            fn_UpdateTrayOngBu_UI();
        }

        // ========================================================
        // [MỚI]: CÁC HÀM LƯU VÀ ĐỌC SỐ MẺ TỰ ĐỘNG TỪ Ổ CỨNG
        // ========================================================
        private void LoadBatchCounters()
        {
            try
            {
                Class_LogicPCtoPLC.BatchCount_Tray1 = Properties.Settings.Default.Batch_Tray1 <= 0 ? 1 : Properties.Settings.Default.Batch_Tray1;
                Class_LogicPCtoPLC.BatchCount_Tray2 = Properties.Settings.Default.Batch_Tray2 <= 0 ? 1 : Properties.Settings.Default.Batch_Tray2;
                Class_LogicPCtoPLC.BatchCount_Tray3 = Properties.Settings.Default.Batch_Tray3 <= 0 ? 1 : Properties.Settings.Default.Batch_Tray3;
                Class_LogicPCtoPLC.BatchCount_TrayErr = Properties.Settings.Default.Batch_TrayErr <= 0 ? 1 : Properties.Settings.Default.Batch_TrayErr;
            }
            catch
            {
                // Nếu chưa tạo biến trong Settings thì gán mặc định bằng 1
                Class_LogicPCtoPLC.BatchCount_Tray1 = 1;
                Class_LogicPCtoPLC.BatchCount_Tray2 = 1;
                Class_LogicPCtoPLC.BatchCount_Tray3 = 1;
                Class_LogicPCtoPLC.BatchCount_TrayErr = 1;
            }
        }

        private void SaveBatchCounters()
        {
            try
            {
                Properties.Settings.Default.Batch_Tray1 = Class_LogicPCtoPLC.BatchCount_Tray1;
                Properties.Settings.Default.Batch_Tray2 = Class_LogicPCtoPLC.BatchCount_Tray2;
                Properties.Settings.Default.Batch_Tray3 = Class_LogicPCtoPLC.BatchCount_Tray3;
                Properties.Settings.Default.Batch_TrayErr = Class_LogicPCtoPLC.BatchCount_TrayErr;
                Properties.Settings.Default.Save(); // Ghi thẳng xuống ổ cứng ngay lập tức
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi lưu Settings Mẻ: " + ex.Message);
            }
        }

        // ========================================================
        // CÁC HÀM XỬ LÝ SỰ KIỆN LOGIC
        // ========================================================
        public void UpdateSystemStateFromPLC()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Class_LogicPCtoPLC.IsGlobal_LayKhay)
                {
                    if (Class_LogicPCtoPLC.Done_In_Out_Grip || isM2003_Idle || isM2004_Stop)
                    {
                        if (!isSafeToTake)
                        {
                            isSafeToTake = true;
                            SetTextIfChanged(txt_trangthai, "TAKE MODE: ROBOT IS SAFE. YOU CAN OUT TRAYS NOW.");

                            fn_UpdateTray1_UI(Class_LogicPCtoPLC.Index_Tray1);
                            fn_UpdateTray2_UI(Class_LogicPCtoPLC.Index_Tray2);
                            fn_UpdateTray3_UI(Class_LogicPCtoPLC.Index_Tray3);
                            fn_UpdateTrayErr_UI(Class_LogicPCtoPLC.Index_TrayErr);
                        }
                    }
                }
                else
                {
                    if (isSafeToTake) isSafeToTake = false;
                }

                if (isM2001_EMG)
                {
                    SetBrushIfChanged(led_Stop, BrushLime);
                    SetBrushIfChanged(led_Reset, BrushGray);
                    SetBrushIfChanged(led_Start, BrushGray);
                }
                else if (isM2002_Alarm)
                {
                    SetBrushIfChanged(led_Reset, BrushLime);
                    SetBrushIfChanged(led_Stop, BrushGray);
                    SetBrushIfChanged(led_Start, BrushGray);
                }
                else if (isM2004_Stop)
                {
                    SetBrushIfChanged(led_Stop, BrushGray);
                    SetBrushIfChanged(led_Start, BrushGray);
                    SetBrushIfChanged(led_Reset, BrushLime);
                }
                else if (isM2003_Idle)
                {
                    SetBrushIfChanged(led_Start, BrushLime);
                    SetBrushIfChanged(led_Stop, BrushGray);
                    SetBrushIfChanged(led_Reset, BrushGray);
                }
                else
                {
                    SetBrushIfChanged(led_Start, BrushLime);
                    SetBrushIfChanged(led_Stop, BrushGray);
                    SetBrushIfChanged(led_Reset, BrushGray);
                }
            });
        }

        public void btn_Start_SCADA_HMI()
        {
            if (!isM2003_Idle)
            {
                MessageBox.Show("Máy chưa ở trạng thái Sẵn sàng (IDLE)!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!isM2005_TraysFull)
            {
                MessageBox.Show("Cảm biến báo CHƯA ĐẦY KHAY! Vui lòng lắp đủ khay trước khi chạy.", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_isWaitingForStart_To_SendM202)
            {
                Class_LogicPCtoPLC.IsGlobal_LayKhay = false;
                Class_LogicPCtoPLC.Signal_UpdatedOffset = false;
                isSafeToTake = false;

                fn_UpdateTray1_UI(Class_LogicPCtoPLC.Index_Tray1);
                fn_UpdateTray2_UI(Class_LogicPCtoPLC.Index_Tray2);
                fn_UpdateTray3_UI(Class_LogicPCtoPLC.Index_Tray3);
                fn_UpdateTrayErr_UI(Class_LogicPCtoPLC.Index_TrayErr);

                MainWindow.Data_transfer!.TagWrite("M202", true);
                MainWindow.Data_transfer!.TagWrite("M201", false);
                SetTextIfChanged(txt_trangthai, "SYSTEM RESUMING (M202 SENT)...");
                _isWaitingForStart_To_SendM202 = false;
            }
            else
            {
                MainWindow.Data_transfer!.TagWrite("Tag_Run", true);
                SetTextIfChanged(txt_trangthai, "SYSTEM RUNNING...");
            }
        }

        private void btn_Start(object sender, RoutedEventArgs e) { btn_Start_SCADA_HMI(); }

        public void Logic_Stop()
        {
            MainWindow.Data_transfer!.TagWrite("M2004", true);
            SetTextIfChanged(txt_trangthai, "STOPPING... WAITING FOR CYCLE TO END.");
        }

        private void btn_Stop(object sender, RoutedEventArgs e) { Logic_Stop(); }

        public void Logic_ResetAll()
        {
            if (isM2001_EMG)
            {
                MessageBox.Show("EMG is holding", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (isM2002_Alarm)
            {
                MainWindow.Data_transfer!.TagWrite("M2002", false);
                MainWindow.Data_transfer!.TagWrite("M2003", true);
                MainWindow.Data_transfer!.TagWrite("M2004", false);

                Class_LogicPCtoPLC.Total_OK = 0;
                Class_LogicPCtoPLC.Total_Err = 0;
                fn_Total_Quantity_UI(0, 0);

                CentrifugeList.Clear();
                for (int i = 0; i < 4; i++)
                {
                    Class_LogicPCtoPLC.Virtual_Cen[i] = new Class_LogicPCtoPLC.TubeData();
                    Class_LogicPCtoPLC.BatchIDs[i] = 0;
                    Class_LogicPCtoPLC.BatchStatus[i] = "";
                }
                Class_LogicPCtoPLC.Index_LT_Place = 0;
                Class_LogicPCtoPLC.Index_LT_Pick = 0;
                MainWindow.Data_transfer!.TagWrite("D50", 0);
                MainWindow.Data_transfer!.TagWrite("D51", 0);
                MainWindow.Data_transfer!.TagWrite("D52", 0);
                MainWindow.Data_transfer!.TagWrite("D53", 0);
                MainWindow.Data_transfer!.TagWrite("D54", 0);

                Class_LogicPCtoPLC.Signal_UpdatedOffset = false;
                isSafeToTake = false;

                // [QUAN TRỌNG]: Không reset số mẻ ở đây để giữ tính liên tục của dữ liệu hệ thống
                SetTextIfChanged(txt_trangthai, "SYSTEM RESET COMPLETED. READY TO START.");
            }
        }

        private void btn_ResetAll_Click(object sender, RoutedEventArgs e) { Logic_ResetAll(); }

        private void btn_AutoMode(object sender, RoutedEventArgs e)
        {
            MainWindow.Data_transfer!.tabManual.IsEnabled = false;
            _isAutoMode = true;
            MainWindow.Data_transfer.TagWrite("Manual_mode", 0);
            frm_setting.Data_transfer?.SetCameraTriggerMode(true);
            MainWindow.Data_transfer.txtMainStatus.Text = "AUTO";
        }

        private async void btn_ManualMode(object sender, RoutedEventArgs e)
        {
            MainWindow.Data_transfer!.tabManual.IsEnabled = true;
            MainWindow.Data_transfer.TagWrite("Manual_mode", 1);
            _isAutoMode = false;
            frm_setting.Data_transfer?.SetCameraTriggerMode(false);
            MainWindow.Data_transfer.txtMainStatus.Text = "MAN";
            // 2. Gửi hàng loạt thông số Acc/Dec từ frm_manual xuống PLC 
            await Task.Run(() =>
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (frm_manual.Data_transfer != null)
                        {
                            // Đọc và gửi Acc/Dec trục X
                           MainWindow.Data_transfer.TagWrite("D30", 1000);
                           MainWindow.Data_transfer.TagWrite("D32", 1000);
                            // Đọc và gửi Acc/Dec trục Y
                            MainWindow.Data_transfer.TagWrite("D34", 800);
                            MainWindow.Data_transfer.TagWrite("D36", 800);
                            // Đọc và gửi Acc/Dec trục Z
                            MainWindow.Data_transfer.TagWrite("D38", 200);
                            MainWindow.Data_transfer.TagWrite("D40", 200);
                            // Đọc và gửi Acc/Dec trục R
                            MainWindow.Data_transfer.TagWrite("D42", 20);
                            MainWindow.Data_transfer.TagWrite("D44", 20);
                            // Đọc và gửi Acc/Dec Servo
                            MainWindow.Data_transfer.TagWrite("D46", 1000);
                            MainWindow.Data_transfer.TagWrite("D48", 1000);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi gửi thông số Acc/Dec: " + ex.Message);
                }
            });

            // 3. GỌI HÀM CỦA FRM_TEACHING ĐỂ ĐẨY TỌA ĐỘ VÀ LY TÂM (HIỆN MESSAGEBOX THÀNH CÔNG)
            if (frm_teaching.Data_transfer != null)
            {
                // Gọi thẳng luồng Async mà không đi qua nút bấm, true = bật MessageBox báo thành công
                await frm_teaching.Data_transfer.Logic_Download_Coordinates_Async(true);
            }


        }

        private void btn_EMG_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.Data_transfer!.TagWrite("M2001", true);
            SetTextIfChanged(txt_trangthai, "EMERGENCY STOP ACTIVATED!");
        }

        private void btn_EMG_Unchecked(object sender, RoutedEventArgs e)
        {
            MainWindow.Data_transfer!.TagWrite("M2001", false);
            MainWindow.Data_transfer!.TagWrite("M2002", true);
            SetTextIfChanged(txt_trangthai, "EMG RELEASED. PLEASE RESET FAULTS.");
        }

        public void Logic_LayKhay()
        {
            Class_LogicPCtoPLC.IsGlobal_LayKhay = true;
            isSafeToTake = false;
            MainWindow.Data_transfer!.TagWrite("M201", true);

            _hasOutTray1 = false;
            _hasOutTray2 = false;
            _hasOutTray3 = false;
            _hasOutTrayErr = false;

            _isWaitingForStart_To_SendM202 = true;

            fn_UpdateTray1_UI(Class_LogicPCtoPLC.Index_Tray1);
            fn_UpdateTray2_UI(Class_LogicPCtoPLC.Index_Tray2);
            fn_UpdateTray3_UI(Class_LogicPCtoPLC.Index_Tray3);
            fn_UpdateTrayErr_UI(Class_LogicPCtoPLC.Index_TrayErr);

            SetTextIfChanged(txt_trangthai, "TAKE MODE: WAITING FOR ROBOT TO BE SAFE (M303)...");
        }
        private void btn_LayKhay_Click(object sender, RoutedEventArgs e) { Logic_LayKhay(); }

        // =========================================================================
        // [MỚI]: TỰ ĐỘNG TĂNG MẺ VÀ LƯU Ổ CỨNG TRONG CÁC HÀM RESET KHAY
        // =========================================================================
        public void Logic_ResetTray1()
        {
            // Cứ bấm reset khay (do đầy khay hoặc bưng khay giữa chừng lúc Take Mode) là tăng 1 mẻ mới
            Class_LogicPCtoPLC.BatchCount_Tray1++;
            SaveBatchCounters(); // Lưu ngay xuống ổ cứng

            Class_LogicPCtoPLC.IsTray1_Full = false;
            Class_LogicPCtoPLC.Index_Tray1 = 0;
            Class_LogicPCtoPLC.fn_UpdateNextOffsetToPLC("Tray1", 0);
            MainWindow.Data_transfer!.TagWrite("D51", 0);

            _hasOutTray1 = true;
            fn_UpdateTray1_UI(0);

            _isWaitingForStart_To_SendM202 = true;
            SetTextIfChanged(txt_trangthai, $"TRAY 1 REPLACED (NOW RUNNING BATCH {Class_LogicPCtoPLC.BatchCount_Tray1}). PRESS START.");
        }
        public void btn_ResetTray1(object sender, RoutedEventArgs e) { Logic_ResetTray1(); }

        public void Logic_ResetTray2()
        {
            Class_LogicPCtoPLC.BatchCount_Tray2++;
            SaveBatchCounters();

            Class_LogicPCtoPLC.IsTray2_Full = false;
            Class_LogicPCtoPLC.Index_Tray2 = 0;
            Class_LogicPCtoPLC.fn_UpdateNextOffsetToPLC("Tray2", 0);
            MainWindow.Data_transfer!.TagWrite("D52", 0);

            _hasOutTray2 = true;
            fn_UpdateTray2_UI(0);

            _isWaitingForStart_To_SendM202 = true;
            SetTextIfChanged(txt_trangthai, $"TRAY 2 REPLACED (NOW RUNNING BATCH {Class_LogicPCtoPLC.BatchCount_Tray2}). PRESS START.");
        }
        public void btn_ResetTray2(object sender, RoutedEventArgs e) { Logic_ResetTray2(); }

        public void Logic_ResetTray3()
        {
            Class_LogicPCtoPLC.BatchCount_Tray3++;
            SaveBatchCounters();

            Class_LogicPCtoPLC.IsTray3_Full = false;
            Class_LogicPCtoPLC.Index_Tray3 = 0;
            Class_LogicPCtoPLC.fn_UpdateNextOffsetToPLC("Tray3", 0);
            MainWindow.Data_transfer!.TagWrite("D53", 0);

            _hasOutTray3 = true;
            fn_UpdateTray3_UI(0);

            _isWaitingForStart_To_SendM202 = true;
            SetTextIfChanged(txt_trangthai, $"TRAY 3 REPLACED (NOW RUNNING BATCH {Class_LogicPCtoPLC.BatchCount_Tray3}). PRESS START.");
        }
        public void btn_ResetTray3(object sender, RoutedEventArgs e) { Logic_ResetTray3(); }

        public void Logic_ResetTrayErr()
        {
            Class_LogicPCtoPLC.BatchCount_TrayErr++;
            SaveBatchCounters();

            Class_LogicPCtoPLC.IsTrayErr_Full = false;
            Class_LogicPCtoPLC.Index_TrayErr = 0;
            Class_LogicPCtoPLC.fn_UpdateNextOffsetToPLC("TrayErr", 0);
            MainWindow.Data_transfer!.TagWrite("D54", 0);

            _hasOutTrayErr = true;
            fn_UpdateTrayErr_UI(0);

            _isWaitingForStart_To_SendM202 = true;
            SetTextIfChanged(txt_trangthai, $"ERROR TRAY REPLACED (NOW RUNNING BATCH {Class_LogicPCtoPLC.BatchCount_TrayErr}). PRESS START.");
        }
        public void btn_ResetTrayErr(object sender, RoutedEventArgs e) { Logic_ResetTrayErr(); }

        // ========================================================
        // TỐI ƯU HÓA RENDER: CÁC HÀM CẬP NHẬT UI
        // ========================================================
        private void SetTextIfChanged(TextBlock tb, string newText)
        {
            if (tb.Text != newText) tb.Text = newText;
        }

        private void SetBrushIfChanged(Shape shape, Brush newBrush)
        {
            if (shape.Fill != newBrush) shape.Fill = newBrush;
        }

        private void SetBorderBgIfChanged(Border border, Brush newBrush)
        {
            if (border.Background != newBrush) border.Background = newBrush;
        }

        public void fn_UpdateTray1_UI(int currentIndex)
        {
            Border[] tray1_slots = { T1_Slot0, T1_Slot1, T1_Slot2, T1_Slot3, T1_Slot4, T1_Slot5, T1_Slot6, T1_Slot7 };
            bool isReadyToTake = Class_LogicPCtoPLC.Tin_hieu_allow_khay || Class_LogicPCtoPLC.IsTray1_Full ||
                                (Class_LogicPCtoPLC.IsGlobal_LayKhay && isSafeToTake && !_hasOutTray1);

            SetBorderBgIfChanged(Nen1, isReadyToTake ? BrushGreen : BrushGray);

            for (int i = 0; i < tray1_slots.Length; i++)
            {
                SetBorderBgIfChanged(tray1_slots[i], i < currentIndex ? BrushOrange : BrushDarkGray);
            }
        }

        public void fn_UpdateTray2_UI(int currentIndex)
        {
            Border[] tray2_slots = { T2_Slot0, T2_Slot1, T2_Slot2, T2_Slot3, T2_Slot4, T2_Slot5, T2_Slot6, T2_Slot7 };
            bool isReadyToTake = Class_LogicPCtoPLC.Tin_hieu_allow_khay || Class_LogicPCtoPLC.IsTray2_Full ||
                                (Class_LogicPCtoPLC.IsGlobal_LayKhay && isSafeToTake && !_hasOutTray2);

            SetBorderBgIfChanged(Nen2, isReadyToTake ? BrushGreen : BrushGray);

            for (int i = 0; i < tray2_slots.Length; i++)
            {
                SetBorderBgIfChanged(tray2_slots[i], i < currentIndex ? BrushOrange : BrushDarkGray);
            }
        }

        public void fn_UpdateTray3_UI(int currentIndex)
        {
            Border[] tray3_slots = { T3_Slot0, T3_Slot1, T3_Slot2, T3_Slot3, T3_Slot4, T3_Slot5, T3_Slot6, T3_Slot7 };
            bool isReadyToTake = Class_LogicPCtoPLC.Tin_hieu_allow_khay || Class_LogicPCtoPLC.IsTray3_Full ||
                                (Class_LogicPCtoPLC.IsGlobal_LayKhay && isSafeToTake && !_hasOutTray3);

            SetBorderBgIfChanged(Nen3, isReadyToTake ? BrushGreen : BrushGray);

            for (int i = 0; i < tray3_slots.Length; i++)
            {
                SetBorderBgIfChanged(tray3_slots[i], i < currentIndex ? BrushOrange : BrushDarkGray);
            }
        }

        public void fn_UpdateTrayErr_UI(int currentIndex)
        {
            Border[] trayErr_slots = { TErr_Slot0, TErr_Slot1, TErr_Slot2, TErr_Slot3, TErr_Slot4, TErr_Slot5, TErr_Slot6, TErr_Slot7 };
            bool isReadyToTake = Class_LogicPCtoPLC.Tin_hieu_allow_khay || Class_LogicPCtoPLC.IsTrayErr_Full ||
                                (Class_LogicPCtoPLC.IsGlobal_LayKhay && isSafeToTake && !_hasOutTrayErr);

            SetBorderBgIfChanged(NenErr, isReadyToTake ? BrushGreen : BrushGray);

            for (int i = 0; i < trayErr_slots.Length; i++)
            {
                SetBorderBgIfChanged(trayErr_slots[i], i < currentIndex ? BrushRed : BrushDarkGray);
            }
        }

        public void fn_UpdateTrayTG_UI()
        {
            Border[] tg_slots = { TG_Slot0, TG_Slot1, TG_Slot2, TG_Slot3, TG_Slot4, TG_Slot5, TG_Slot6, TG_Slot7, TG_Slot8 };
            for (int i = 0; i < tg_slots.Length; i++)
            {
                SetBorderBgIfChanged(tg_slots[i], Class_LogicPCtoPLC.VirtualTray_TG[i].ID != 0 ? BrushOrange : BrushDarkGray);
            }
        }

        public void fn_UpdateCentrifuge_UI(int indexPlace, int indexPick, int quantity, bool trang_thai_ly_tam)
        {
            SetTextIfChanged(txt_Speed, "SPEED:\n" + frm_teaching.Data_transfer!.txtCenVel.Text);
            SetTextIfChanged(txt_Time, "TIME:\n" + frm_teaching.Data_transfer.txtCenTime.Text);

            if (trang_thai_ly_tam)
            {
                // Vẫn giữ biến cờ này để tránh lệnh đổi màu bị gọi lặp đi lặp lại gây tốn CPU
                if (!_isCentrifugeAnimating)
                {
                    SetTextIfChanged(StatusCen, "Status:\nRunning");
                    SetBorderBgIfChanged(thanh1, BrushGreen);
                    SetBorderBgIfChanged(thanh2, BrushGreen);
                    SetBrushIfChanged(bg_LT, BrushLightGreen);
                    bg_LT.Stroke = Brushes.Green;

                    // ĐÃ BỎ: Lệnh tạo và chạy animation chuyển động xoay (DoubleAnimation)
                    _isCentrifugeAnimating = true;
                }
            }
            else
            {
                if (_isCentrifugeAnimating)
                {
                    SetTextIfChanged(StatusCen, "Status:\nStop");
                    SetBorderBgIfChanged(thanh1, BrushStopGray);
                    SetBorderBgIfChanged(thanh2, BrushStopGray);
                    SetBrushIfChanged(bg_LT, Brushes.Transparent);
                    bg_LT.Stroke = Brushes.Black;

                    // ĐÃ BỎ: Lệnh dừng animation (cenRotation.BeginAnimation)
                    _isCentrifugeAnimating = false;
                }
            }

            // Cập nhật màu sắc các lỗ chứa ống nghiệm trên mâm
            Ellipse[] centrifuge_slots = { elTube1_Centrifuge, elTube2_Centrifuge, elTube3_Centrifuge, elTube4_Centrifuge };
            for (int i = 0; i < centrifuge_slots.Length; i++)
            {
                int tubeId = Class_LogicPCtoPLC.Virtual_Cen[i].ID;
                Brush targetBrush = BrushDarkGray;
                if (tubeId == 9999) targetBrush = BrushBlue;
                else if (tubeId != 0) targetBrush = BrushOrange;

                SetBrushIfChanged(centrifuge_slots[i], targetBrush);
            }
        }

        public void fn_UpdateTrayOngBu_UI()
        {
            Border[] ob_slots = { Tube1_TG, Tube2_TG, Tube3_TG };
            for (int i = 0; i < ob_slots.Length; i++)
            {
                SetBorderBgIfChanged(ob_slots[i], Class_LogicPCtoPLC.VirtualTray_OngBu[i].ID != 0 ? BrushBlue : BrushDarkGray);
            }
        }

        public void fn_UpdateCentrifuge_Table_UI()
        {
            if (Class_LogicPCtoPLC.Dang_Ly_tam == true && _State_lytam == false)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Class_LogicPCtoPLC.BatchStatus[i] == "Waiting")
                        Class_LogicPCtoPLC.BatchStatus[i] = "Spinning";
                }
                _State_lytam = true;
            }
            else if (Class_LogicPCtoPLC.Dang_Ly_tam == false && _State_lytam == true)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Class_LogicPCtoPLC.BatchStatus[i] == "Spinning")
                        Class_LogicPCtoPLC.BatchStatus[i] = "Waiting Check Level";
                }
                _State_lytam = false;
            }

            var tempList = new List<CenTubeDisplay>();
            for (int i = 0; i < 4; i++)
            {
                int id = Class_LogicPCtoPLC.BatchIDs[i];
                if (id != 0)
                {
                    if (id == 9999)
                        tempList.Add(new CenTubeDisplay { TubeSlot = $"tube {i + 1}", Barcode = "ỐNG BÙ", Status = "" });
                    else
                        tempList.Add(new CenTubeDisplay { TubeSlot = $"tube {i + 1}", Barcode = id.ToString(), Status = Class_LogicPCtoPLC.BatchStatus[i] });
                }
            }

            bool isChanged = false;
            if (CentrifugeList.Count != tempList.Count)
                isChanged = true;
            else
            {
                for (int i = 0; i < tempList.Count; i++)
                {
                    if (CentrifugeList[i].Barcode != tempList[i].Barcode || CentrifugeList[i].Status != tempList[i].Status)
                    {
                        isChanged = true;
                        break;
                    }
                }
            }

            if (isChanged)
            {
                CentrifugeList.Clear();
                foreach (var item in tempList) CentrifugeList.Add(item);
            }
        }

        public void fn_Total_Quantity_UI(short tong_loi, short tong_ok)
        {
            SetTextIfChanged(txt_TotalOk, tong_ok.ToString());
            SetTextIfChanged(txt_TotalErr, tong_loi.ToString());
            SetTextIfChanged(txt_Total, (tong_loi + tong_ok).ToString());
        }

        public void fn_UpdateLiquidLevel_UI(double? plasmaVol, double? rbcVol, string status)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                SetTextIfChanged(txt_MainPlasmaVol, plasmaVol.HasValue ? $"Plasma: {plasmaVol.Value:F1} ml" : "Plasma: -- ml");
                SetTextIfChanged(txt_MainRbcVol, rbcVol.HasValue ? $"RBC: {rbcVol.Value:F1} ml" : "RBC: -- ml");
                SetTextIfChanged(txt_MainLiquidStatus, $"Status: {status}");

                txt_MainLiquidStatus.Foreground = status == "OK" ? BrushLime : BrushRed;

                if (plasmaVol.HasValue && rbcVol.HasValue && (plasmaVol.Value > 0 || rbcVol.Value > 0))
                {
                    double maxVol = 5.0;
                    double total = plasmaVol.Value + rbcVol.Value;
                    if (total > maxVol) maxVol = total;

                    double emptyPct = 1.0 - (total / maxVol);
                    double plasmaPct = emptyPct + (plasmaVol.Value / maxVol);

                    LinearGradientBrush tubeBrush = new LinearGradientBrush();
                    tubeBrush.StartPoint = new Point(0, 0);
                    tubeBrush.EndPoint = new Point(0, 1);

                    tubeBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
                    tubeBrush.GradientStops.Add(new GradientStop(Colors.White, emptyPct));
                    tubeBrush.GradientStops.Add(new GradientStop(Colors.Gold, emptyPct));
                    tubeBrush.GradientStops.Add(new GradientStop(Colors.Gold, plasmaPct));
                    tubeBrush.GradientStops.Add(new GradientStop(Colors.DarkRed, plasmaPct));
                    tubeBrush.GradientStops.Add(new GradientStop(Colors.DarkRed, 1.0));

                    tubeBrush.Freeze(); // Đóng băng brush Gradient
                    bdr_VisualTube.Background = tubeBrush;
                }
                else
                {
                    bdr_VisualTube.Background = Brushes.White;
                }
            });
        }
    }
}