using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatService.Api.Services
{
    // Cấp Singleton: Trái tim điều phối Mạng nhện Online (Thread-safe)
    public class ConnectionTracker
    {
        // 1 UserId (Key) -> Nhiều máy tính (Value là List ống nước)
        // Bọc lock() Tường đồng vách sắt: Hàng trăm luồng RAM đâm vào không rớt giọt Data nào
        private readonly Dictionary<string, List<string>> _onlineUsers = new();

        public Task<bool> AddConnection(string userId, string connectionId)
        {
            bool isFirstDevice = false;
            lock (_onlineUsers)
            {
                if (!_onlineUsers.ContainsKey(userId))
                {
                    // Lần đầu thằng này đăng nhập, tạo Rổ trống cho nó
                    _onlineUsers.Add(userId, new List<string>());
                    isFirstDevice = true;
                }
                // Thêm thiết bị mới (Ví dụ login điện thoại -> Nhét thêm 1 ống nước)
                _onlineUsers[userId].Add(connectionId);
            }
            return Task.FromResult(isFirstDevice);
        }

        public Task<bool> RemoveConnection(string userId, string connectionId)
        {
            bool isGlobalOffline = false;
            lock (_onlineUsers)
            {
                if (!_onlineUsers.ContainsKey(userId)) return Task.FromResult(isGlobalOffline);

                _onlineUsers[userId].Remove(connectionId);
                
                // Nếu thằng này tắt 3G trên điện thoại, gập màn hình Laptop... (Rổ trống trơn)
                if (_onlineUsers[userId].Count == 0)
                {
                    _onlineUsers.Remove(userId); // Giải phóng RAM cho máy chủ
                    isGlobalOffline = true;
                }
            }
            return Task.FromResult(isGlobalOffline);
        }

        public Task<string[]> GetConnectionsForUser(string userId)
        {
            string[] connectionIds = Array.Empty<string>();
            lock (_onlineUsers)
            {
                if (_onlineUsers.ContainsKey(userId))
                {
                    // Xuất dàn ống nước của thằng này ra Array để súng liên thanh bắn 1 lúc
                    connectionIds = _onlineUsers[userId].ToArray();
                }
            }
            return Task.FromResult(connectionIds);
        }
    }
}
