using System.Windows;
using System.Windows.Input;

namespace SCADA_VERTEX
{
    public partial class frm_JogPopup : Window
    {
        public frm_JogPopup()
        {
            InitializeComponent();
        }

        // --- TRỤC X --- (Trong manual bạn viết true/false)
        private void btnJogAheadX_pressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogAheadX", true);
        private void btnJogAheadX_notpressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogAheadX", false);
        private void btnJogBackX_pressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogBackX", true);
        private void btnJogBackX_notpressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogBackX", false);

        // --- TRỤC Y --- (Trong manual bạn viết 1/0)
        private void btnJogAheadY_pressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogAheadY", 1);
        private void btnJogAheadY_notpressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogAheadY", 0);
        private void btnJogBackY_pressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogBackY", 1);
        private void btnJogBackY_notpressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogBackY", 0);

        // --- TRỤC Z ---
        private void btnJogAheadZ_pressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogAheadZ", 1);
        private void btnJogAheadZ_notpressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogAheadZ", 0);
        private void btnJogBackZ_pressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogBackZ", 1);
        private void btnJogBackZ_notpressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogBackZ", 0);

        // --- TRỤC R ---
        private void btnJogAheadR_pressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogAheadR", 1);
        private void btnJogAheadR_notpressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogAheadR", 0);
        private void btnJogBackR_pressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogBackR", 1);
        private void btnJogBackR_notpressed(object sender, MouseButtonEventArgs e) => MainWindow.Data_transfer.TagWrite("Tag_JogBackR", 0);
    }
}
