    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
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
    using ZXing.QrCode.Internal;

    namespace SCADA_VERTEX
    {
        /// <summary>
        /// Interaction logic for frm_manual.xaml
        /// </summary>
        public partial class frm_manual : UserControl
        {
            internal static frm_manual? Data_transfer;
            public frm_manual()
            {
                InitializeComponent();
                Data_transfer = this;
            }

            #region 1. Các sự kiện Group Actuator
            // xy lanh A
            private void btnOnXLA(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_CylinderA", 1);
                //frm_setting.Data_transfer?.SetCameraTriggerMode(true);
                //if (frm_main.Data_transfer != null)
                //{
                //    frm_main.Data_transfer._isDebugging = true;
                //}

        }
            private void btnOffXLA(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_CylinderA", 0);
                
            }
            // Xy lanh B
            private void btnOnXLB(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_CylinderB", 1);
                //double? rbc = null;
                //double? plasma = null;
                //string failReason = "ERR";

                //(_, _, rbc, plasma, _, failReason) = frm_setting.Data_transfer!.liquid_reading_auto();
            }
            private void btnOffXLB(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_CylinderB", 0);
            }
        // DC1
            private void btnOnDC1(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_DC1", 1);
            }
            private void btnOffDC1(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_DC1", 0);
            }
            //DC2
            private void btnOnDC2(object sender, RoutedEventArgs e)
            {
                // Ghi giá trị xuống PLC
                MainWindow.Data_transfer?.TagWrite("Tag_DC2", 1);        
            }
        private void btnOffDC2(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_DC2", 0);
            }
            #endregion

            #region 2. Các sự kiện nút Group trục X
            // trang thai step 1
            private void btnOn1_pressed(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("ON_Axis1", true);
            }

            private void btnOn1_notpressed(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("ON_Axis1", false);
            }
            // nut jog+ truc X
            private void btnJogAheadX_pressed(object sender, MouseButtonEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogAheadX", true);
            }
            private void btnJogAheadX_notpressed(object sender, MouseEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogAheadX", false);
            }
            // nut jog- truc X
            private void btnJogBackX_pressed(object sender, MouseButtonEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogBackX", true);
            }
            private void btnJogBackX_notpressed(object sender, MouseEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogBackX", false);
            }
            // nut Home truc X
            private void btnJogHomeX_Click(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_HomeX", 1);
            }
            // NHẬP TỐC ĐỘ JOG TRỤC X (ẤN ENTER ĐỂ LƯU)
            private void txtJogSpeedX_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    if (sender is TextBox txt)
                    {
                        // Chuyển sang đọc số thực (double) và xử lý cả dấu phẩy/chấm
                        if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newSpeed))
                        {
                            try
                            {
                                // Tốc độ thực (mm/s) quy đổi ra tần số xung (Hz - dạng số nguyên)
                                int frequencyX = (int)Math.Round((newSpeed * 800.0) / 8.0);
                                MainWindow.Data_transfer.TagWrite("Tag_JogSpeedX", frequencyX);
                                Keyboard.ClearFocus();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Data error: " + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please enter a valid number (e.g., 12.5)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }

            private void txtAccDecX_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    if (sender is TextBox txt)
                    {
                        // Chuyển sang đọc số thực (double) và xử lý cả dấu phẩy/chấm
                        if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newAccX))
                        {
                            try
                            {
                                int AccX = (int)newAccX;
                                MainWindow.Data_transfer.TagWrite("D30", AccX);
                                MainWindow.Data_transfer.TagWrite("D32", AccX);
                                Keyboard.ClearFocus();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Data error: " + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please enter a valid number (e.g., 12.5)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }


        #endregion

            #region 3. Các sự kiện nút Group trục Y
        // trang thai step
        private void btnOn2_pressed(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("ON_Axis2", 1);
            }

            private void btnOn2_notpressed(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("ON_Axis2", 0);
            }
            // nut jog+ truc Y
            private void btnJogAheadY_pressed(object sender, MouseButtonEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogAheadY", 1);
            }

            private void btnJogAheadY_notpressed(object sender, MouseEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogAheadY", 0);
            }

            // nut jog- truc Y
            private void btnJogBackY_pressed(object sender, MouseButtonEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogBackY", 1);
            }

            private void btnJogBackY_notpressed(object sender, MouseEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogBackY", 0);
            }

            // nut Home truc Y
            private void btnJogHomeY_Click(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_HomeY", 1);
            }

            // NHẬP TỐC ĐỘ JOG TRỤC Y (ẤN ENTER ĐỂ LƯU)
            private void txtJogSpeedY_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    if (sender is TextBox txt)
                    {
                        // Chuyển sang đọc số thực (double) và xử lý cả dấu phẩy/chấm
                        if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newSpeed))
                        {
                            try
                            {
                                // Tốc độ thực (mm/s) quy đổi ra tần số xung (Hz - dạng số nguyên)
                                int frequencyY = (int)Math.Round((newSpeed * 800.0) / 8.0);
                                MainWindow.Data_transfer.TagWrite("Tag_JogSpeedY", frequencyY);
                                Keyboard.ClearFocus();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Data error: " + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please enter a valid number (e.g., 12.5)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }

            private void txtAccDecY_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    if (sender is TextBox txt)
                    {
                        // Chuyển sang đọc số thực (double) và xử lý cả dấu phẩy/chấm
                        if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newAccY))
                        {
                            try
                            {
                            int AccY = (int)newAccY;
                                MainWindow.Data_transfer.TagWrite("D34", AccY);
                            MainWindow.Data_transfer.TagWrite("D36", AccY);
                            Keyboard.ClearFocus();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Data error: " + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please enter a valid number (e.g., 12.5)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
        #endregion

            #region 4. Các sự kiện nút Group trục Z
            private void btnOn3_pressed(object sender, RoutedEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("ON_Axis3", 1);
                }

                private void btnOn3_notpressed(object sender, RoutedEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("ON_Axis3", 0);
                }
                // nut jog+ truc Z
                private void btnJogAheadZ_pressed(object sender, MouseButtonEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_JogAheadZ", 1);
                }

                private void btnJogAheadZ_notpressed(object sender, MouseEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_JogAheadZ", 0);
                }

                // nut jog- truc Z
                private void btnJogBackZ_pressed(object sender, MouseButtonEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_JogBackZ", 1);
                }

                private void btnJogBackZ_notpressed(object sender, MouseEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_JogBackZ", 0);
                }

                // nut Home truc Z
                private void btnJogHomeZ_Click(object sender, RoutedEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_HomeZ", 1);
                }


                // NHẬP TỐC ĐỘ JOG TRỤC Z (ẤN ENTER ĐỂ LƯU)
                private void txtJogSpeedZ_KeyDown(object sender, KeyEventArgs e)
                {
                    if (e.Key == Key.Enter)
                    {
                        if (sender is TextBox txt)
                        {
                            // Chuyển sang đọc số thực (double) và xử lý cả dấu phẩy/chấm
                            if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newSpeed))
                            {
                                try
                                {
                                    // Tốc độ thực (mm/s) quy đổi ra tần số xung (Hz - dạng số nguyên)
                                    int frequencyZ = (int)Math.Round((newSpeed * 800.0) / 8.0);
                                    MainWindow.Data_transfer.TagWrite("Tag_JogSpeedZ", frequencyZ);
                                    Keyboard.ClearFocus();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Data error: " + ex.Message);
                                }
                            }
                            else
                            {
                                MessageBox.Show("Please enter a valid number (e.g., 12.5)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }

            private void txtAccDecZ_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    if (sender is TextBox txt)
                    {
                        // Chuyển sang đọc số thực (double) và xử lý cả dấu phẩy/chấm
                        if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newAccZ))
                        {
                            try
                            {
                                int AccZ = (int)newAccZ;
                                MainWindow.Data_transfer.TagWrite("D38", AccZ);
                                MainWindow.Data_transfer.TagWrite("D40", AccZ);
                            Keyboard.ClearFocus();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Data error: " + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please enter a valid number (e.g., 12.5)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            #endregion

            #region 5. Các sự kiện nút Group trục xoay R
            private void btnOn4_pressed(object sender, RoutedEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("ON_Axis4", 1);
                }

                private void btnOn4_notpressed(object sender, RoutedEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("ON_Axis4", 0);
                }
                // nut jog+ truc R
                private void btnJogAheadR_pressed(object sender, MouseButtonEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_JogAheadR", 1);
                }

                private void btnJogAheadR_notpressed(object sender, MouseEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_JogAheadR", 0);
                }

                // nut jog- truc R
                private void btnJogBackR_pressed(object sender, MouseButtonEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_JogBackR", 1);
                }

                private void btnJogBackR_notpressed(object sender, MouseEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_JogBackR", 0);
                }

                // nut Home truc R
                private void btnJogHomeR_Click(object sender, RoutedEventArgs e)
                {
                    MainWindow.Data_transfer.TagWrite("Tag_HomeR", 1);
                }

                // NHẬP TỐC ĐỘ JOG TRỤC R (ĐƠN VỊ: RPM - ẤN ENTER ĐỂ LƯU)
                private void txtJogSpeedR_KeyDown(object sender, KeyEventArgs e)
                {
                    if (e.Key == Key.Enter)
                    {
                        if (sender is TextBox txt)
                        {
                            if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newRPM))
                            {
                                try
                                {
                                    int pulsesPerRev = 800; // Cài đặt trên driver động cơ bước
                                    // Tốc độ (RPM) quy đổi ra tần số xung (Hz - dạng số nguyên)
                                    int frequencyR = (int)Math.Round((newRPM / 60.0) * pulsesPerRev);

                                    MainWindow.Data_transfer.TagWrite("Tag_JogSpeedR", frequencyR);
                                    Keyboard.ClearFocus();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Data error: " + ex.Message);
                                }
                            }
                            else
                            {
                                MessageBox.Show("Please enter a valid number (e.g., 12.5)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }

                // NHẬP THỜI GIAN TĂNG/GIẢM TỐC (ACC/DEC) TRỤC R - ẤN ENTER ĐỂ LƯU
                private void txtAccDecR_KeyDown(object sender, KeyEventArgs e)
                {
                    if (e.Key == Key.Enter)
                    {
                        if (sender is TextBox txt)
                        {
                            // Chuyển sang đọc số thực (double) và xử lý cả dấu phẩy/chấm
                            if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newAccR))
                            {
                                try
                                {
                                    int AccR = (int)newAccR;
                                    MainWindow.Data_transfer.TagWrite("D42", AccR);
                                    MainWindow.Data_transfer.TagWrite("D44", AccR);
                                    Keyboard.ClearFocus();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Data error: " + ex.Message);
                                }
                            }
                            else
                            {
                                MessageBox.Show("Please enter a valid number (e.g., 100)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }

                #endregion

            #region 6. Hàm xử lý sự kiện cho Servo
            // nút On
            private void btnOnServo_pressed(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_OnServo", 1);
            }
            private void btnOnServo_notpressed(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_OnServo", 0);
            }

            // nút Home
            private void btnJogHomeServo_Click(object sender, RoutedEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_HomeServo", 1);
            }


            // nút Jog+
            private void btnJogAheadServo_pressed(object sender, MouseButtonEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogAheadServo", 1);
            }

            private void btnJogAheadServo_notpressed(object sender, MouseEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogAheadServo", 0);
            }
            // nút Jog-
            private void btnJogBackServo_pressed(object sender, MouseButtonEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogBackServo", 1);
            }

            private void btnJogBackServo_notpressed(object sender, MouseEventArgs e)
            {
                MainWindow.Data_transfer.TagWrite("Tag_JogBackServo", 0);
            }

            // textbox Jogspeed
            private void txtJogSpeedServo_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    if (sender is TextBox txt)
                    {
                        if (double.TryParse(txt.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newRPM))
                        {
                            try
                            {
                                int pulsesPerRev = 800; // Cài đặt trên driver động cơ bước
                                // Tốc độ (RPM) quy đổi ra tần số xung (Hz - dạng số nguyên)
                                int frequencyS = (int)Math.Round((newRPM / 60.0) * pulsesPerRev);

                                MainWindow.Data_transfer.TagWrite("Tag_JogSpeedServo", frequencyS);
                                Keyboard.ClearFocus();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Data error: " + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please enter a valid number (e.g., 12.5)!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            //textbox Acc/Dec
            private void txtServoAccDec_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    if (sender is TextBox txt) // Đã sửa gộp ép kiểu và check null
                    {
                        if (int.TryParse(txt.Text, out int newRPM))
                        {
                            try
                            {
                                MainWindow.Data_transfer.TagWrite("D46", newRPM);
                                MainWindow.Data_transfer.TagWrite("D48", newRPM);
                            Keyboard.ClearFocus();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Data error: " + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please enter only integers!", "Input warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            #endregion

            #region 7. Hàm hiển thị các giá trị từ PLC

            // 1. Hàm hiển thị giá trị tag lên textbox
            public void fn_Tags_Manual_To_Textbox()
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtCurrentPosX.Text = Class_Common.PosTrucX.ToString("F2");
                    txtCurrentPosY.Text = Class_Common.PosTrucY.ToString("F2");
                    txtCurrentPosZ.Text = Class_Common.PosTrucZ.ToString("F2");
                    txtCurrentPosR.Text = Class_Common.PosTrucR.ToString("F2");
                    txtServoCurrentPos.Text = Class_Common.PosServo.ToString("F2");
                    txtServoCurrentSpeed.Text = Class_Common.SpeedServo.ToString("F2");
                });
            }
        #endregion
        }
    }