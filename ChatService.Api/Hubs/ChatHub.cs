using System.Security.Claims;
using ChatService.Api.Data;
using ChatService.Api.Models;
using ChatService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatService.Api.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatDbContext _context;
        private readonly ConnectionTracker _tracker;

        // Bơm ống tiêm Tracker vào Trung Tâm
        public ChatHub(ChatDbContext context, ConnectionTracker tracker)
        {
            _context = context;
            _tracker = tracker;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? Context.User?.FindFirst("sub")?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                var isFirstDevice = await _tracker.AddConnection(userId, Context.ConnectionId);
                
                // Cải tiến Đỉnh Cao: Chỉ sủa thông báo "Online" DUY NHẤT khi nó Mở máy đầu tiên.
                // Mở máy 2 thì Im lặng. Nhờ vậy chặn Spam sập màn hình thiên hạ.
                if (isFirstDevice)
                {
                    await Clients.Others.SendAsync("UserCameOnline", userId);
                }
            }
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? Context.User?.FindFirst("sub")?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                var isGlobalOffline = await _tracker.RemoveConnection(userId, Context.ConnectionId);
                
                // Mẹo nhãn tiền: Tắt Laptop nhưng chưa tắt Đt thì vẫn giấu trạng thái "Offline"
                if (isGlobalOffline)
                {
                    await Clients.Others.SendAsync("UserWentOffline", userId);
                }
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessageToUser(string receiverId, string messageContent)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? Context.User?.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(senderId)) return;

            // 1. Lưu CSDL Vĩnh Viễn
            var chatMessage = new ChatMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = messageContent,
                SentAt = DateTime.UtcNow
            };
            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            // 2. Móc ống súng 2 nòng: Quét TẤT CẢ Tọa Độ (Điện thoại, IPad, Laptop) của Địch dập tin nhắn 1 lượt
            var receiverConnections = await _tracker.GetConnectionsForUser(receiverId);
            if (receiverConnections.Length > 0)
            {
                await Clients.Clients(receiverConnections).SendAsync("ReceivePrivateMessage", senderId, messageContent, chatMessage.SentAt);
            }
            
            // 3. Phản pháo về mọi Thiết bị của chính MÌNH (Send từ Phone -> Hiện cả Inbox của Laptop) do đồng bộ State
            var senderConnections = await _tracker.GetConnectionsForUser(senderId);
            if (senderConnections.Length > 0)
            {
                await Clients.Clients(senderConnections).SendAsync("ReceivePrivateMessage", senderId, messageContent, chatMessage.SentAt);
            }
        }

        // === TÍNH NĂNG CHAT NHÓM (GROUP CHAT) ===

        // 1. Gia nhập Nhóm
        public async Task JoinGroup(string groupName)
        {
            // SignalR có sẵn lõi Quản lý Nhóm siêu tốc, ép Ống Nước này thuộc về 1 Group
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Ai đó";
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", groupName, "Hệ Thống", $"{senderId} đã bay vào phòng!");
        }

        // 2. Rời Nhóm
        public async Task LeaveGroup(string groupName)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Ai đó";
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", groupName, "Hệ Thống", $"{senderId} đã chuồn khỏi Nhóm!");
            
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        // 3. Bắn tin nhắn chùm (Broadcast) vào toàn bộ những ai đang trong Room
        public async Task SendMessageToGroup(string groupName, string messageContent)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? Context.User?.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(senderId)) return;

            // Lưu Database vĩnh viễn
            var chatMessage = new ChatMessage
            {
                SenderId = senderId,
                GroupName = groupName, // Ghi nhận ID của Nhóm thay vì Receiver cá nhân
                Content = messageContent,
                SentAt = DateTime.UtcNow
            };
            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            // Sức mạnh lớn nhất của SignalR: Bơm tin 1 phát nảy đồng loạt hàng ngàn Client
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", groupName, senderId, messageContent, chatMessage.SentAt);
        }

        // CHIÊU MÔN [2]: BÙ ĐẮP TIN NHẮN (HỒI PHỤC KHI ĐỨT CÁP)
        // Frontend sẽ gọi hàm này và gửi lên mốc "Thời gian nhận tin cuối cùng" (lastMessageTime)
        public async Task SyncMissedMessages(DateTime? lastMessageTime)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? Context.User?.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            var query = _context.ChatMessages
                .Where(m => m.ReceiverId == userId || m.SenderId == userId);
                
            if (lastMessageTime.HasValue)
            {
                // Móc SẠCH SẼ các tin nhắn (riêng tư) người khác gửi tới trong quãng thời gian nó bị rớt mạng tới nay
                query = query.Where(m => m.SentAt > lastMessageTime.Value);
            }
            else
            {
                query = query.Take(0); // Nếu ko đưa mốc thời gian thì ko bù tin nào sất (hoặc móc lịch sử bình thường)
            }
            
            var missedMessages = await query.OrderBy(m => m.SentAt).ToListAsync();

            if (missedMessages.Any())
            {
                // Bơm nguyên 1 cục Bù Đắp dội ngược xuống giao diện rớt mạng của kẻ ngắt kết nối
                await Clients.Caller.SendAsync("ReceiveMissedMessages", missedMessages);
            }
        }
    }
}
