using SCADA_VERTEX;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace SCADA_VERTEX
{
    /// <summary>
    /// Interaction logic for frm_Login.xaml
    /// </summary>
    public partial class frm_Login : Window
    {
        public frm_Login()
        {
            InitializeComponent();

        }
        #region 1. Hàm cho form log-in
        // Hàm cho nút nhấn đăng nhập
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            // Lấy dữ liệu từ ô nhập liệu
            string username = txtUsername.Text.Trim(); // Loại bỏ khoảng trắng đầu & cuối
            string password = txtPassword.Password;

            // Danh sách tài khoản hợp lệ
            Dictionary<string, string> users = new Dictionary<string, string>()
            {
                { "Admin", "1" },
                { "Danh", "1" },
                { "Do", "1" },
                { "Tien", "1" }
            };

            // Kiểm tra đăng nhập
            if (users.ContainsKey(username) && users[username] == password)
            {
                // Gửi username sang Main Windows
                MainWindow.Data_transfer.fn_Set_UserName(username);
                this.Close();
            }
            else
            {
                lblMessage.Text = "Sai tài khoản hoặc mật khẩu!";
            }
        }
        #endregion
    }
}
