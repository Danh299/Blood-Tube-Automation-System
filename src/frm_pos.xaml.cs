using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SCADA_VERTEX
{
    public partial class frm_pos : UserControl
    {
        internal static frm_pos? Data_transfer;

        public frm_pos()
        {
            InitializeComponent();
            Data_transfer = this;
        }

        #region 1. Hiển thị UI Tab (Cập nhật vị trí thực từ Class_Common)
        public void fn_Tags_Pos_To_Textbox()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                txtCurrentPosX.Text = Class_Common.PosTrucX.ToString("F2");
                txtCurrentPosY.Text = Class_Common.PosTrucY.ToString("F2");
                txtCurrentPosZ.Text = Class_Common.PosTrucZ.ToString("F2");
                txtCurrentPosR.Text = Class_Common.PosTrucR.ToString("F2");
                txtCurrentPosServo.Text = Class_Common.PosServo.ToString("F2");
            });
        }
        #endregion

        #region 2. Các sự kiện Trục X (Axis X)
        private void btnOnX_Checked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("ON_Axis1", true);
        private void btnOnX_Unchecked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("ON_Axis1", false);
        private void btnHomeX_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("Tag_HomeX", 1);
        private void btnMoveX_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("moveX", 1);

        private void txtTargetPosX_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double targetMm))
                {
                    int targetPulse = (int)Math.Round((targetMm * 800.0) / 8.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_BeltX", targetPulse);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtSpeedX_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double speed))
                {
                    int freq = (int)Math.Round((speed * 800.0) / 8.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_JogSpeedX", freq);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtAccX_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int accTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D30", accTime); // D30 = Accel Time X
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtDecX_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int decTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D32", decTime); // D32 = Decel Time X
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region 3. Các sự kiện Trục Y (Axis Y)
        private void btnOnY_Checked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("ON_Axis2", 1);
        private void btnOnY_Unchecked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("ON_Axis2", 0);
        private void btnHomeY_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("Tag_HomeY", 1);
        private void btnMoveY_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("moveY", 1);

        private void txtTargetPosY_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double targetMm))
                {
                    int targetPulse = (int)Math.Round((targetMm * 800.0) / 8.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_BeltY", targetPulse);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtSpeedY_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double speed))
                {
                    int freq = (int)Math.Round((speed * 800.0) / 8.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_JogSpeedY", freq);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtAccY_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int accTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D34", accTime); // D34 = Accel Time Y
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtDecY_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int decTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D36", decTime); // D36 = Decel Time Y
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region 4. Các sự kiện Trục Z (Axis Z)
        private void btnOnZ_Checked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("ON_Axis3", 1);
        private void btnOnZ_Unchecked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("ON_Axis3", 0);
        private void btnHomeZ_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("Tag_HomeZ", 1);
        private void btnMoveZ_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("moveZ", 1);

        private void txtTargetPosZ_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double targetMm))
                {
                    // Đã sửa lỗi làm tròn thập phân: (targetMm * 800) / 8
                    int targetPulse = (int)Math.Round((targetMm * 800.0) / 8.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_BeltZ", targetPulse);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtSpeedZ_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double speed))
                {
                    int freq = (int)Math.Round((speed * 800.0) / 8.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_JogSpeedZ", freq);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtAccZ_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int accTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D38", accTime); // D38 = Accel Time Z
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtDecZ_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int decTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D40", decTime); // D40 = Decel Time Z
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region 5. Các sự kiện Trục xoay R (Axis R)
        private void btnOnR_Checked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("ON_Axis4", 1);
        private void btnOnR_Unchecked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("ON_Axis4", 0);
        private void btnHomeR_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("Tag_HomeR", 1);
        private void btnMoveR_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("moveR", 1);

        private void txtTargetPosR_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double targetDeg))
                {
                    int targetPulse = (int)Math.Round((targetDeg * 800.0) / 360.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_TargetPosR", targetPulse);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtSpeedR_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double newRPM))
                {
                    int freq = (int)Math.Round((newRPM / 60.0) * 800.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_SpeedPosR", freq);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtAccR_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int accTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D42", accTime); // D42 = Accel Time R
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtDecR_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int decTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D44", decTime); // D44 = Decel Time R
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region 6. Các sự kiện Trục Servo (Servo Motor)
        private void btnServoOn_Checked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("Tag_OnServo", 1);
        private void btnServoOn_Unchecked(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("Tag_OnServo", 0);
        private void btnServoHome_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("Tag_HomeServo", 1);
        private void btnMoveServo_Click(object sender, RoutedEventArgs e) => MainWindow.Data_transfer?.TagWrite("moveServo", 1);

        private void txtTargetPosServo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double targetDeg))
                {
                    // Đã sửa lỗi làm tròn thập phân: (targetDeg * 800) / 360
                    int targetPulse = (int)Math.Round((targetDeg * 800.0) / 360.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_TargetPosServo", targetPulse);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtSpeedServo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (double.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double newRPM))
                {
                    int freq = (int)Math.Round((newRPM / 60.0) * 800.0);
                    MainWindow.Data_transfer?.TagWrite("Tag_JogSpeedServo", freq);
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter a valid number!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtAccServo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int accTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D46", accTime); // D46 = Accel Time Servo
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtDecServo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt)
            {
                if (int.TryParse(txt.Text, out int decTime))
                {
                    MainWindow.Data_transfer?.TagWrite("D48", decTime); // D48 = Decel Time Servo
                    Keyboard.ClearFocus();
                }
                else MessageBox.Show("Please enter integer!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion
    }
}