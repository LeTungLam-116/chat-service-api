using System.Security.Claims;
using ChatService.Api.Data;
using ChatService.Api.Models;
using ChatService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
                
                // MỚI [Phase 5]: Quét CSDL xem nó Thuộc những Băng Đảng (Groups) nào thì Tự Động đẩy ống nghe (ConnectionId) cho nó nghe lén toàn bộ!
                var myGroups = await _context.GroupMembers
                    .Where(gm => gm.UserId == userId && gm.IsPendingApproval == false)
                    .Select(gm => gm.GroupId)
                    .ToListAsync();
                foreach (var gId in myGroups)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, gId);
                }

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

        public async Task<ChatMessage?> SendMessageToUser(string receiverId, string messageContent)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? Context.User?.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(senderId)) return null;

            // Kiểm duyệt nội dung tin nhắn tránh spam hoặc lỗi
            if (string.IsNullOrWhiteSpace(messageContent) || messageContent.Length > 2000) return null;

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

            // 2. Sử dụng SignalR built-in User để phát sóng đến toàn bộ thiết bị của người gửi & người nhận
            await Clients.User(receiverId).SendAsync("ReceivePrivateMessage", chatMessage.Id, senderId, messageContent, chatMessage.SentAt, chatMessage.IsRevoked, chatMessage.IsRead);
            await Clients.User(senderId).SendAsync("ReceivePrivateMessage", chatMessage.Id, senderId, messageContent, chatMessage.SentAt, chatMessage.IsRevoked, chatMessage.IsRead);

            return chatMessage;
        }

        // === TÍNH NĂNG [THU HỒI TIN NHẮN] ===
        public async Task RevokeMessage(Guid messageId)
        {
            var myId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? Context.User?.FindFirst("sub")?.Value;
            
            var msg = await _context.ChatMessages.FindAsync(messageId);
            if (msg == null || msg.SenderId != myId) return; // Kháng lỗi Hacker: Chỉ cho phép thu hồi tin của chính mình

            msg.IsRevoked = true;
            await _context.SaveChangesAsync();

            // Phát lệnh thu hồi sang TẤT CẢ thiết bị của mình & đối phương thông qua Built-in User Mapping
            if (!string.IsNullOrEmpty(msg.ReceiverId))
            {
                await Clients.User(msg.ReceiverId).SendAsync("MessageRevoked", messageId);
            }
            await Clients.User(msg.SenderId).SendAsync("MessageRevoked", messageId);
        }

        // === TÍNH NĂNG [ĐÃ XEM] ===
        public async Task MarkAsRead(Guid messageId, string senderOfMessageId)
        {
            var myId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? Context.User?.FindFirst("sub")?.Value;

            var msg = await _context.ChatMessages.FindAsync(messageId);
            if (msg == null || msg.ReceiverId != myId) return; // Kháng lỗi: Chỉ update khi mình là Thằng Nhận
            
            msg.IsRead = true;
            await _context.SaveChangesAsync();

            // Bắn tia "Đã xem" ngược về thiết bị của Người Gửi (Báo nó biết là dòng này đã bị Seen!)
            await Clients.User(senderOfMessageId).SendAsync("MessageRead", messageId);
        }

        // === TÍNH NĂNG [ĐANG GÕ...] ===
        public async Task TypingToggled(string receiverId, bool isTyping)
        {
            var myId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? Context.User?.FindFirst("sub")?.Value;
                           
            await Clients.User(receiverId).SendAsync("UserTyping", myId, isTyping);
        }

        // === TÍNH NĂNG CHAT NHÓM (GROUP CHAT) ===

        // 1. Gia nhập Nhóm: Không xài nữa vì đã Bê sang DB quản lý, lúc OnConnected nó tự nhận!
        public async Task JoinGroupFallback(string groupName) { await Task.CompletedTask; }

        public async Task LeaveGroupFallback(string groupName) { await Task.CompletedTask; }

        // 3. Bắn tin nhắn chùm (Broadcast) vào toàn bộ những ai đang trong Room
        public async Task<ChatMessage?> SendMessageToGroup(string groupName, string messageContent)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? Context.User?.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(senderId)) return null;

            // Kiểm duyệt nội dung tin nhắn tránh spam hoặc lỗi
            if (string.IsNullOrWhiteSpace(messageContent) || messageContent.Length > 2000) return null;

            // Vành Đai Mới: Quét DB xem có đúng nó là Môn đệ của Bang chúa không? Kẻo hacker fake lệnh!
            var isMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupName && gm.UserId == senderId && gm.IsPendingApproval == false);
            if (!isMember) return null;

            var user = await _context.Users.FindAsync(senderId);

            // Lưu Database vĩnh viễn
            var chatMessage = new ChatMessage
            {
                SenderId = senderId,
                GroupName = groupName,
                Content = messageContent,
                SentAt = DateTime.UtcNow
            };
            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            // Nhồi đủ thứ (Id, Tên, Avatar) bắn cho cả Lò cùng nghe
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", chatMessage.Id, groupName, senderId, user?.DisplayName ?? "Ai đó", user?.AvatarUrl ?? "", messageContent, chatMessage.SentAt, chatMessage.IsRevoked, chatMessage.IsRead);
            return chatMessage;
        }

        // CHIÊU MÔN [2]: BÙ ĐẮP TIN NHẮN (HỒI PHỤC KHI ĐỨT CÁP)
        // Frontend sẽ gọi hàm này và gửi lên mốc "Thời gian nhận tin cuối cùng" (lastMessageTime)
        public async Task SyncMissedMessages(DateTime? lastMessageTime)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? Context.User?.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            if (!lastMessageTime.HasValue) return;

            // 1. Quét xem user này đang thuộc những nhóm (Group) hợp lệ nào
            var myGroups = await _context.GroupMembers
                .Where(gm => gm.UserId == userId && gm.IsPendingApproval == false)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            // 2. Móc sạch sẽ các tin nhắn (riêng tư hoặc nhóm) phát sinh sau mốc thời gian mất kết nối
            var missedMessages = await _context.ChatMessages
                .Where(m => (m.ReceiverId == userId || m.SenderId == userId || (m.GroupName != null && myGroups.Contains(m.GroupName))) 
                            && m.SentAt > lastMessageTime.Value)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            if (missedMessages.Any())
            {
                // Điền thông tin Người Gửi (DisplayName, Avatar) để frontend hiển thị chính xác
                var senderIds = missedMessages.Select(m => m.SenderId).Distinct().ToList();
                var senders = await _context.Users
                    .Where(u => senderIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.AvatarUrl });

                var result = missedMessages.Select(m => new {
                    id = m.Id,
                    senderId = m.SenderId,
                    senderName = senders.TryGetValue(m.SenderId, out var s) ? s.DisplayName : "Ai đó...",
                    senderAvatar = senders.TryGetValue(m.SenderId, out var sa) ? sa.AvatarUrl : "",
                    receiverId = m.ReceiverId,
                    groupName = m.GroupName,
                    content = m.Content,
                    sentAt = m.SentAt,
                    isMine = m.SenderId == userId,
                    isRevoked = m.IsRevoked,
                    isRead = m.IsRead
                }).ToList();

                // Bơm nguyên 1 cục Bù Đắp dội ngược xuống giao diện rớt mạng của kẻ ngắt kết nối
                await Clients.Caller.SendAsync("ReceiveMissedMessages", result);
            }
        }
    }
}
