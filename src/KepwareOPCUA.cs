using System;
using System.Threading.Tasks; // BẮT BUỘC THÊM THƯ VIỆN NÀY ĐỂ CHẠY ĐA LUỒNG
using Opc.UaFx.Client;

namespace SCADA_VERTEX
{
    public class KepwareOPCUA
    {
        private OpcClient _opcClient;
        private string _nodePrefix;

        public void OPCSetting(string ioServer, string channel, int scanTime, object tagsList)
        {
            string safeOpcUrl = ioServer;

            // Nếu Windows lôi từ cache ra chuỗi cũ không có chữ opc.tcp:// thì ép nó về mặc định luôn
            if (string.IsNullOrEmpty(safeOpcUrl) || !safeOpcUrl.StartsWith("opc.tcp://"))
            {
                safeOpcUrl = "opc.tcp://127.0.0.1:49320";
                Console.WriteLine("Đã phát hiện và chặn đứng lỗi Cache Settings!");
            }

            _opcClient = new OpcClient(safeOpcUrl);

            // Xử lý chuỗi Channel
            _nodePrefix = "ns=2;s=" + channel;
            if (!_nodePrefix.EndsWith("."))
            {
                _nodePrefix += ".";
            }
        }

        public void Connect()
        {
            if (_opcClient != null)
            {
                _opcClient.Connect();
            }
        }

        public void Disconnect()
        {
            if (_opcClient != null)
            {
                _opcClient.Disconnect();
            }
        }

        // ====================================================================
        // HÀM ĐỌC: Vẫn giữ nguyên chạy đồng bộ (Synchronous)
        // Lý do: Hàm này được gọi bởi PLC_Timer (đã nằm sẵn dưới luồng nền).
        // Cứ để nó đọc đồng bộ để đảm bảo lấy được dữ liệu rồi mới chạy tiếp.
        // ====================================================================
        public T Read<T>(string tagName)
        {
            try
            {
                // Nếu chưa kết nối, trả về giá trị mặc định an toàn (0 cho số, false cho bool)
                if (_opcClient == null || _opcClient.State != OpcClientState.Connected)
                    return default(T);

                string fullNodeId = _nodePrefix + tagName;
                var nodeValue = _opcClient.ReadNode(fullNodeId);

                if (nodeValue != null && nodeValue.Value != null)
                {
                    // Bí quyết: Tự động convert giá trị thô của OPC UA về đúng kiểu T bạn yêu cầu
                    return (T)Convert.ChangeType(nodeValue.Value, typeof(T));
                }

                return default(T);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi đọc tag {tagName} sang kiểu {typeof(T).Name}: {ex.Message}");
                return default(T);
            }
        }

        // ====================================================================
        // HÀM GHI (TỐI ƯU LUỒNG): Tự động ném tác vụ xuống Background Thread
        // Áp dụng "Fire-and-Forget": Giao diện ra lệnh ghi xong là quên luôn,
        // không bị "đứng hình" chờ mạng phản hồi.
        // ====================================================================
        public void Write(string tagName, object value)
        {
            // Đẩy toàn bộ quá trình đọc-ép-kiểu-rồi-ghi xuống luồng nền
                try
                {
                    if (_opcClient != null && _opcClient.State == OpcClientState.Connected)
                    {
                        string fullNodeId = _nodePrefix + tagName;

                        // TUYỆT CHIÊU: Đọc tag từ Kepware trước để xem nó đang đòi hỏi kiểu dữ liệu gì
                        var currentNode = _opcClient.ReadNode(fullNodeId);

                        if (currentNode != null && currentNode.Value != null)
                        {
                            // 1. Lấy kiểu dữ liệu chuẩn của Kepware
                            Type targetType = currentNode.Value.GetType();

                            // 2. Tự động "ép" cái value bạn truyền vào về đúng kiểu Kepware cần
                            object safeValue = Convert.ChangeType(value, targetType);

                            // 3. Ghi xuống PLC
                            _opcClient.WriteNode(fullNodeId, safeValue);
                        }
                        else
                        {
                            // Nếu tag chưa có dữ liệu, cứ ghi mặc định
                            _opcClient.WriteNode(fullNodeId, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // In ra chi tiết lỗi dưới dạng ngầm để dễ kiểm tra, không hiện popup làm phiền người dùng
                    Console.WriteLine($"Lỗi ghi tag {tagName} - Chi tiết: {ex.Message}");
                }
        }
    }
}