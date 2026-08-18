using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace SCADA_VERTEX
{
    /// <summary>
    /// Interaction logic for frm_HIS.xaml
    /// </summary>
    public partial class frm_HIS : UserControl
    {
        // Lấy chuỗi kết nối đã lưu trong Setting của dự án
        string connString = Properties.Settings.Default.SQL_String;

        public frm_HIS()
        {
            InitializeComponent();
            LoadDataFromSQL();
        }

        // ============================================================
        // 1. HÀM TẢI DỮ LIỆU TỪ SQL SERVER LÊN BẢNG HIỂN THỊ
        // ============================================================
        private void LoadDataFromSQL()
        {
            if (string.IsNullOrEmpty(connString)) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    // Truy vấn lấy các cột thông tin bệnh nhân
                    string query = @"SELECT SampleID AS [Sample ID], 
                                            PatientName AS [Patient Name], 
                                            TestType AS [Test Type], 
                                            CentrifugeNeeded AS [Centrifuge], 
                                            Result AS [Result], 
                                            CreationTime AS [Time Created] 
                                     FROM Tbl_PatientProfile 
                                     ORDER BY CreationTime DESC"; // Ưu tiên hiện mẫu mới nhất

                    SqlDataAdapter da = new SqlDataAdapter(query, conn);
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    dgvPatientList.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải dữ liệu SQL:\n" + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // 2. NÚT SAVE: LƯU HỒ SƠ MỚI
        // ============================================================
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtID.Text) || string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Vui lòng nhập ID và Tên bệnh nhân!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    // Sử dụng GETDATE() để SQL tự động ghi lại thời gian tạo
                    string sqlInsert = @"INSERT INTO Tbl_PatientProfile (SampleID, PatientName, TestType, CentrifugeNeeded, Result, CreationTime) 
                                         VALUES (@id, @name, @type, @cen, @res, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(sqlInsert, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", txtID.Text.Trim());
                        cmd.Parameters.AddWithValue("@name", txtName.Text.Trim());
                        cmd.Parameters.AddWithValue("@type", cboType.Text);
                        cmd.Parameters.AddWithValue("@cen", cboCentrifuge.Text);
                        cmd.Parameters.AddWithValue("@res", txtResult.Text.Trim());

                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Lưu hồ sơ bệnh nhân thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDataFromSQL();
                btnClear_Click(null!, null!);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi lưu dữ liệu (ID có thể đã tồn tại):\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // 3. NÚT UPDATE: CẬP NHẬT THÔNG TIN
        // ============================================================
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtID.Text)) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    string sqlUpdate = @"UPDATE Tbl_PatientProfile 
                                         SET PatientName = @name, 
                                             TestType = @type, 
                                             CentrifugeNeeded = @cen, 
                                             Result = @res 
                                         WHERE SampleID = @id";

                    using (SqlCommand cmd = new SqlCommand(sqlUpdate, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", txtID.Text.Trim());
                        cmd.Parameters.AddWithValue("@name", txtName.Text.Trim());
                        cmd.Parameters.AddWithValue("@type", cboType.Text);
                        cmd.Parameters.AddWithValue("@cen", cboCentrifuge.Text);
                        cmd.Parameters.AddWithValue("@res", txtResult.Text.Trim());

                        int check = cmd.ExecuteNonQuery();
                        if (check > 0) MessageBox.Show("Cập nhật thành công!", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                LoadDataFromSQL();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi cập nhật:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // 4. NÚT DELETE: XÓA DỮ LIỆU
        // ============================================================
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtID.Text)) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa mẫu: {txtID.Text}?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connString))
                    {
                        conn.Open();
                        string sqlDelete = "DELETE FROM Tbl_PatientProfile WHERE SampleID = @id";
                        using (SqlCommand cmd = new SqlCommand(sqlDelete, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", txtID.Text.Trim());
                            cmd.ExecuteNonQuery();
                        }
                    }
                    LoadDataFromSQL();
                    btnClear_Click(null!, null!);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi xóa:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ============================================================
        // 5. NÚT CLEAR: LÀM TRỐNG FORM NHẬP LIỆU
        // ============================================================
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            txtID.Text = "";
            txtName.Text = "";
            txtResult.Text = "";
            cboType.SelectedIndex = 0;
            cboCentrifuge.SelectedIndex = 0;

            txtID.IsReadOnly = false; // Mở khóa ID để nhập mới
            dgvPatientList.SelectedItem = null;
        }

        // ============================================================
        // 6. SỰ KIỆN CHỌN DÒNG TRÊN BẢNG (SYNC DỮ LIỆU LÊN FORM)
        // ============================================================
        private void dgvPatientList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvPatientList.SelectedItem is DataRowView row)
            {
                txtID.Text = row["Sample ID"].ToString();
                txtName.Text = row["Patient Name"].ToString();
                cboType.Text = row["Test Type"].ToString();
                cboCentrifuge.Text = row["Centrifuge"].ToString();
                txtResult.Text = row["Result"].ToString();

                // Khóa ô Sample ID lại vì không được phép sửa Khóa chính
                txtID.IsReadOnly = true;
            }
        }
    }
}