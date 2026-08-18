using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Threading;

namespace SCADA_VERTEX
{
    public partial class MainWindow : Window
    {
        internal static MainWindow Data_transfer;

        frm_setting f_setting = new frm_setting();
        frm_teaching f_teaching = new frm_teaching();
        frm_manual f_manual = new frm_manual();
        frm_Logs f_logs = new frm_Logs();
        frm_alarm f_alarm = new frm_alarm();
        frm_main f_main = new frm_main();
        frm_HIS f_his = new frm_HIS();
        frm_pos f_pos = new frm_pos();

        public MainWindow()
        {
            InitializeComponent();
            StartClock();
            this.Loaded += Window_Loaded;
            Data_transfer = this;
        }

        private bool[] lastAlarmStates = new bool[16];
        private string[] alarmNames = {
            "TRAY 1 FULL", "TRAY 2 FULL", "TRAY 3 FULL", "TRAY ERR FULL",
            "TAKE TRAY", "NO TRAY", "EMERGENCY IS HOLDING", "SERVO ERROR", "LOSS CONNECT DATABASE"
        };
        private string[] alarmCodes = { "I01", "I02", "I03", "I04", "I05", "W01", "E01", "E02", "E03" };

        private string _lastDisplayedMessage = "";
        private int _scanCycles = 0; // BỘ ĐẾM TRỄ KHỞI ĐỘNG CHỐNG BÁO ẢO

        public void fn_UpdateAlarms_And_Messages()
        {
            bool[] currentAlarms = new bool[16];

            currentAlarms[0] = Class_LogicPCtoPLC.IsTray1_Full;
            currentAlarms[1] = Class_LogicPCtoPLC.IsTray2_Full;
            currentAlarms[2] = Class_LogicPCtoPLC.IsTray3_Full;
            currentAlarms[3] = Class_LogicPCtoPLC.IsTrayErr_Full;
            currentAlarms[4] = Class_LogicPCtoPLC.IsGlobal_LayKhay;

            if (frm_main.Data_transfer != null)
            {
                currentAlarms[5] = !frm_main.Data_transfer.isM2005_TraysFull;
                currentAlarms[6] = frm_main.Data_transfer.isM2001_EMG;
            }

            currentAlarms[7] = false;
            currentAlarms[8] = false;

            int d70_BitValue = 0;
            for (int i = 0; i < 9; i++)
            {
                if (currentAlarms[i])
                {
                    d70_BitValue |= (1 << i);
                }
            }
            TagWrite("D70", d70_BitValue);

            for (int i = 0; i < 9; i++)
            {
                if (currentAlarms[i] == true)
                {
                    if (lastAlarmStates[i] == false)
                    {
                        if (i >= 6) SaveAlarmToSQL(alarmNames[i], "Please check system physically", alarmCodes[i]);
                    }

                    if (frm_alarm.Data_transfer != null)
                        frm_alarm.Data_transfer.AddCurrentAlarm(alarmNames[i], "Please check system physically", alarmCodes[i]);
                }
                else
                {
                    if (frm_alarm.Data_transfer != null)
                        frm_alarm.Data_transfer.RemoveCurrentAlarm(alarmCodes[i]);
                }

                lastAlarmStates[i] = currentAlarms[i];
            }

            string newMsgToDisplay = "";
            bool isFatal = false;
            bool isWarning = false;

            if (currentAlarms[6]) { newMsgToDisplay = "EMERGENCY IS HOLDING! PLEASE RELEASE AND RESET."; isFatal = true; }
            else if (currentAlarms[7]) { newMsgToDisplay = "SERVO ERROR DETECTED!"; isFatal = true; }
            else if (currentAlarms[8]) { newMsgToDisplay = "DATABASE CONNECTION LOST!"; isFatal = true; }
            else if (currentAlarms[5]) { newMsgToDisplay = "NO TRAY DETECTED. PLEASE INSERT TRAYS."; isWarning = true; }
            else if (currentAlarms[4])
            {
                if (Class_LogicPCtoPLC.Done_In_Out_Grip == false)
                    newMsgToDisplay = "TAKE TRAY MODE: WAITING FOR ROBOT TO BE SAFE ...";
                else
                    newMsgToDisplay = "TAKE TRAY MODE: ROBOT IS SAFE. PLEASE REMOVE TRAYS.";
                isWarning = true;
            }
            else if (currentAlarms[0]) { newMsgToDisplay = "TRAY 1 IS FULL. PLEASE OUT TRAY."; isWarning = true; }
            else if (currentAlarms[1]) { newMsgToDisplay = "TRAY 2 IS FULL. PLEASE OUT TRAY."; isWarning = true; }
            else if (currentAlarms[2]) { newMsgToDisplay = "TRAY 3 IS FULL. PLEASE OUT TRAY."; isWarning = true; }
            else if (currentAlarms[3]) { newMsgToDisplay = "ERROR TRAY IS FULL. PLEASE OUT TRAY."; isWarning = true; }
            else
            {
                newMsgToDisplay = frm_main.Data_transfer != null && frm_main.Data_transfer.isM2004_Stop ? "SYSTEM STOPPED." : "SYSTEM IS IDLE / RUNNING NORMAL.";
            }

            if (newMsgToDisplay != _lastDisplayedMessage)
            {
                _lastDisplayedMessage = newMsgToDisplay;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtGlobalTime.Text = $"[{DateTime.Now:HH:mm:ss}]";
                    ShowGlobalMsg(newMsgToDisplay, isFatal, isWarning);
                });
            }
        }

        private void ShowGlobalMsg(string msg, bool isFatalError, bool isWarning = false)
        {
            txtGlobalMessage.Text = msg;

            if (isFatalError)
            {
                bdrGlobalMessage.Background = new SolidColorBrush(Colors.DarkRed);
                txtGlobalMessage.Foreground = Brushes.White;
                iconGlobal.Foreground = Brushes.White;
                iconGlobal.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircleOutline;
            }
            else if (isWarning)
            {
                bdrGlobalMessage.Background = new SolidColorBrush(Colors.Orange);
                txtGlobalMessage.Foreground = Brushes.Black;
                iconGlobal.Foreground = Brushes.Black;
                iconGlobal.Kind = MaterialDesignThemes.Wpf.PackIconKind.BellAlertOutline;
            }
            else
            {
                bdrGlobalMessage.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
                txtGlobalMessage.Foreground = Brushes.Black;
                iconGlobal.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0054A6"));
                iconGlobal.Kind = MaterialDesignThemes.Wpf.PackIconKind.InformationOutline;
            }
        }

        private void SaveAlarmToSQL(string alarmName, string solution, string alarmCode)
        {
            try
            {
                string connStr = Properties.Settings.Default.SQL_String;
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string query = "INSERT INTO Tbl_AlarmLog (Time, AlarmName, Solution, AlarmCode) VALUES (@time, @name, @sol, @code)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@time", DateTime.Now);
                        cmd.Parameters.AddWithValue("@name", alarmName);
                        cmd.Parameters.AddWithValue("@sol", solution);
                        cmd.Parameters.AddWithValue("@code", alarmCode);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi ghi Alarm SQL: " + ex.Message);
            }
        }

        #region 1. Hàm hiển thị cửa sổ đăng nhập
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            frm_Login loginWindow = new frm_Login();
            loginWindow.ShowDialog();

            fn_OPC();
            Class_Common.OnPLCScan += PLC_Timer;
            int scantime = Properties.Settings.Default.PLC_Tags_Scan_Time;
            Class_Common.Timer_PLCTagscan(scantime);
        }
        public void fn_Set_UserName(string username)
        {
            txtUserName.Text = "User: " + username;
            tbl_Role.Text = "ROLE: " + username;
        }
        private void btn_Login_Click(object sender, RoutedEventArgs e)
        {
            frm_Login loginWindow = new frm_Login();
            loginWindow.ShowDialog();
        }
        #endregion

        #region 2 Hàm hiển thị thời gian thực của hệ
        private void StartClock()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (sender, args) =>
            {
                txtclock.Text = DateTime.Now.ToString(" | dd/MM/yyyy HH:mm:ss ");
            };
            timer.Start();
        }
        #endregion

        #region 3. Hàm chuyển đổi các tab menu
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.Source is TabControl)) return;

            if (tabSetting != null && tabSetting.IsSelected) { frameSetting.Content = f_setting; }
            else if (tabTeaching != null && tabTeaching.IsSelected) { frameTeaching.Content = f_teaching; }
            else if (tabManual != null && tabManual.IsSelected) { frameManual.Content = f_manual; }
            else if (tabPos != null && tabPos.IsSelected) { framePos.Content = f_pos; }
            else if (tabLogs != null && tabLogs.IsSelected) { frameLogs.Content = f_logs; }
            else if (tabAlarm != null && tabAlarm.IsSelected) { frameAlarm.Content = f_alarm; }
            else if (tabMain != null && tabMain.IsSelected) { frameMain.Content = f_main; }
            else if (tabHIS != null && tabHIS.IsSelected) { frameHIS.Content = f_his; }
        }
        #endregion

        #region 4. Hàm kết nối c# với OPC
        KepwareOPCUA PLC1 = new KepwareOPCUA();

        public void fn_OPC()
        {
            string IOServer = Properties.Settings.Default.IOServer;
            string Channel = Properties.Settings.Default.Channel;
            int PLCscantime = Properties.Settings.Default.PLC_Tags_Scan_Time;

            PLC1.OPCSetting(IOServer, Channel, PLCscantime, Class_Tags_List.Tags_List);
            PLC1.Connect();
            Console.WriteLine("KẾT NỐI KEPWARE THÀNH CÔNG!");
        }

        public void fn_OPCConect()
        {
            try { PLC1.Connect(); } catch (Exception ex) { Console.WriteLine(ex); }
        }
        public void fn_OPCDisconect()
        {
            try { PLC1.Disconnect(); } catch (Exception ex) { Console.WriteLine(ex); }
        }
        #endregion

        #region 5. Hàm timer quét tags (Chạy dưới Background Thread)
        private void PLC_Timer()
        {
            fn_Tags_Read();
            fn_TabMenu_Tags_Read();
            fn_Event_Tags_Read();
            Class_LogicPCtoPLC.fn_type_offset_2();
            // CHỈ QUÉT ALARM SAU 1.5 GIÂY ĐỂ OPC LẤY DỮ LIỆU CHUẨN, KHÔNG BỊ "NO TRAY" ẢO
            _scanCycles++;
            if (_scanCycles > 15)
            {
                fn_UpdateAlarms_And_Messages();
            }
        }
        #endregion

        #region 6. Hàm đọc giá trị Tags
        public void fn_Tags_Read()
        {
            string tag_watchdog = PLC1.Read<string>("watchdog");
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (tabSetting != null && tabSetting.IsSelected == true)
                {
                    f_setting?.Fn_Show_txt_Watchdog(tag_watchdog);
                    frm_setting.Data_transfer!.fn_connect_status(tag_watchdog);
                }
            }));
        }
        #endregion

        #region 7. Hàm ghi và đọc giá trị Tag (Wrapper an toàn cho toàn hệ thống)
        // Hàm GHI dữ liệu xuống Kepware/PLC
        public void TagWrite(string tag, object value)
        {
            try
            {
                PLC1.Write(tag, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI GHI OPC] Tag: {tag} | {ex.Message}");
            }
        }

        // Hàm ĐỌC dữ liệu Generic tổng quát
        public T TagRead<T>(string tag)
        {
            try
            {
                return PLC1.Read<T>(tag);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI ĐỌC OPC] Tag: {tag} | {ex.Message}");
                return default(T)!;
            }
        }
        #endregion

        #region 8. Hàm đọc giá trị tag các tab menu
        public void fn_TabMenu_Tags_Read()
        {
            double PosX_Raw = PLC1.Read<double>("Tag_PosX");
            double PosY_Raw = PLC1.Read<double>("Tag_PosY");
            double PosZ_Raw = PLC1.Read<double>("Tag_PosZ");
            double PosR_Raw = PLC1.Read<double>("Tag_PosR");
            double PosServo_Raw = PLC1.Read<double>("Tag_PosServo");
            double SpeedServo_Raw = PLC1.Read<double>("Tag_SpeedServo");

            Class_Common.PosTrucX = (PosX_Raw * 8.0) / 800.0;
            Class_Common.PosTrucY = (PosY_Raw * 8.0) / 800.0;
            Class_Common.PosTrucZ = (PosZ_Raw * 8.0) / 800.0;

            double totalDegreesR = (PosR_Raw * 360.0) / 800.0;
            double displayDegreesR = totalDegreesR % 360.0;
            if (displayDegreesR < 0) displayDegreesR += 360.0;
            Class_Common.PosTrucR = displayDegreesR;

            double totalDegreesServo = (PosServo_Raw * 360.0) / 800.0;
            double displayDegreesServo = totalDegreesServo % 360.0;
            if (displayDegreesServo < 0) displayDegreesServo += 360.0;

            Class_Common.PosServo = displayDegreesServo;
            Class_Common.SpeedServo = (int)((SpeedServo_Raw * 60.0) / 800.0);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (tabManual != null && tabManual.IsSelected == true)
                {
                    frm_manual.Data_transfer!.fn_Tags_Manual_To_Textbox();
                }
                if (tabTeaching != null && tabTeaching.IsSelected == true)
                {
                    frm_teaching.Data_transfer!.fn_Tags_Teaching_To_Textbox();
                }

                if (tabPos != null && tabPos.IsSelected == true)
                {
                    frm_pos.Data_transfer!.fn_Tags_Pos_To_Textbox();
                }

                if (tabMain != null && tabMain.IsSelected == true)
                {
                    frm_main.Data_transfer!.fn_UpdateTray1_UI(Class_LogicPCtoPLC.Index_Tray1);
                    frm_main.Data_transfer.fn_UpdateTray2_UI(Class_LogicPCtoPLC.Index_Tray2);
                    frm_main.Data_transfer.fn_UpdateTray3_UI(Class_LogicPCtoPLC.Index_Tray3);
                    frm_main.Data_transfer.fn_UpdateTrayErr_UI(Class_LogicPCtoPLC.Index_TrayErr);
                    frm_main.Data_transfer.fn_UpdateCentrifuge_UI(Class_LogicPCtoPLC.Index_LT_Place, Class_LogicPCtoPLC.Index_LT_Pick, Class_LogicPCtoPLC.Quantity_TubeMain, Class_LogicPCtoPLC.Dang_Ly_tam);
                    frm_main.Data_transfer.fn_UpdateTrayTG_UI();
                    frm_main.Data_transfer.fn_Total_Quantity_UI(Class_LogicPCtoPLC.Total_Err, Class_LogicPCtoPLC.Total_OK);
                    frm_main.Data_transfer.fn_UpdateCentrifuge_Table_UI();
                    frm_main.Data_transfer.fn_UpdateTrayOngBu_UI();
                    frm_main.Data_transfer.UpdateSystemStateFromPLC();

                    bool trigger_Start = PLC1.Read<bool>("M90");
                    bool trigger_Stop = PLC1.Read<bool>("M91");
                    bool trigger_ResetAll = PLC1.Read<bool>("M92");
                    bool trigger_Take = PLC1.Read<bool>("M93");

                    if (trigger_Start) { frm_main.Data_transfer.btn_Start_SCADA_HMI(); TagWrite("M90", false); }
                    if (trigger_Stop) { frm_main.Data_transfer.Logic_Stop(); TagWrite("M91", false); }
                    if (trigger_ResetAll) { frm_main.Data_transfer.Logic_ResetAll(); TagWrite("M92", false); }
                    if (trigger_Take) { frm_main.Data_transfer.Logic_LayKhay(); TagWrite("M93", false); }

                    bool hmiResetTray1 = PLC1.Read<bool>("M2100");
                    bool hmiResetTray2 = PLC1.Read<bool>("M2101");
                    bool hmiResetTray3 = PLC1.Read<bool>("M2102");
                    bool hmiResetTrayErr = PLC1.Read<bool>("M2103");

                    if (hmiResetTray1) { frm_main.Data_transfer.Logic_ResetTray1(); TagWrite("M2100", false); }
                    if (hmiResetTray2) { frm_main.Data_transfer.Logic_ResetTray2(); TagWrite("M2101", false); }
                    if (hmiResetTray3) { frm_main.Data_transfer.Logic_ResetTray3(); TagWrite("M2102", false); }
                    if (hmiResetTrayErr) { frm_main.Data_transfer.Logic_ResetTrayErr(); TagWrite("M2103", false); }
                }
            }));
        }
        #endregion

        #region 9. Đọc tất cả các miền nhớ cần thiết từ PLC để xử lý sự kiện
        public void fn_Event_Tags_Read()
        {
            Class_LogicPCtoPLC.M0 = PLC1.Read<bool>("M0");
            Class_LogicPCtoPLC.M1 = PLC1.Read<bool>("M1");
            Class_LogicPCtoPLC.M2 = PLC1.Read<bool>("M2");
            Class_LogicPCtoPLC.M3 = PLC1.Read<bool>("M3");
            Class_LogicPCtoPLC.M4 = PLC1.Read<bool>("M4");
            Class_LogicPCtoPLC.M5 = PLC1.Read<bool>("M5");
            Class_LogicPCtoPLC.M6 = PLC1.Read<bool>("M6");
            Class_LogicPCtoPLC.M7 = PLC1.Read<bool>("M7");
            Class_LogicPCtoPLC.M8 = PLC1.Read<bool>("M8");
            Class_LogicPCtoPLC.M9 = PLC1.Read<bool>("M9");
            Class_LogicPCtoPLC.M10 = PLC1.Read<bool>("M10");
            Class_LogicPCtoPLC.M11 = PLC1.Read<bool>("M11");
            Class_LogicPCtoPLC.M12 = PLC1.Read<bool>("M12");
            Class_LogicPCtoPLC.M13 = PLC1.Read<bool>("M13");
            Class_LogicPCtoPLC.M14 = PLC1.Read<bool>("M14");
            Class_LogicPCtoPLC.M15 = PLC1.Read<bool>("M15");
            Class_LogicPCtoPLC.M16 = PLC1.Read<bool>("M16");
            Class_LogicPCtoPLC.M17 = PLC1.Read<bool>("M17");
            Class_LogicPCtoPLC.M18 = PLC1.Read<bool>("M18");
            Class_LogicPCtoPLC.M19 = PLC1.Read<bool>("M19");
            Class_LogicPCtoPLC.M20 = PLC1.Read<bool>("M20");
            Class_LogicPCtoPLC.M21 = PLC1.Read<bool>("M21");
            Class_LogicPCtoPLC.M22 = PLC1.Read<bool>("M22");
            Class_LogicPCtoPLC.M23 = PLC1.Read<bool>("M23");
            Class_LogicPCtoPLC.M24 = PLC1.Read<bool>("M24");
            Class_LogicPCtoPLC.M25 = PLC1.Read<bool>("M25");
            Class_LogicPCtoPLC.M26 = PLC1.Read<bool>("M26");
            Class_LogicPCtoPLC.M27 = PLC1.Read<bool>("M27");
            Class_LogicPCtoPLC.M28 = PLC1.Read<bool>("M28");
            Class_LogicPCtoPLC.M91 = PLC1.Read<bool>("M91");
            Class_LogicPCtoPLC.Dang_Ly_tam = PLC1.Read<bool>("M100");
            Class_LogicPCtoPLC.Done_In_Out_Grip = PLC1.Read<bool>("M303");
            Class_LogicPCtoPLC.Status_processing = PLC1.Read<int>("D2");
            Class_LogicPCtoPLC.Quantity_TubeMain = PLC1.Read<int>("D3");
            Class_LogicPCtoPLC.Tin_hieu_allow_khay = PLC1.Read<bool>("M302");
            Class_LogicPCtoPLC.isM108_Requested = PLC1.Read<bool>("M108");
            frm_main.Data_transfer!.isM2001_EMG = PLC1.Read<bool>("M2001");
            frm_main.Data_transfer!.isM2002_Alarm = PLC1.Read<bool>("M2002");
            frm_main.Data_transfer!.isM2003_Idle = PLC1.Read<bool>("M2003");
            frm_main.Data_transfer!.isM2004_Stop = PLC1.Read<bool>("M2004");
            frm_main.Data_transfer!.isM2005_TraysFull = PLC1.Read<bool>("M120");
        }
        #endregion
    }
}