using ChatService.Api.Data;
using ChatService.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatService.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatsController : ControllerBase
    {
        private readonly ChatDbContext _context;

        public ChatsController(ChatDbContext context)
        {
            _context = context;
        }

        [HttpGet("{partnerId}")]
        public async Task<IActionResult> GetConversationHistory(string partnerId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var messages = await _context.ChatMessages
                .Where(m => 
                    (m.SenderId == myId && string.Equals(m.ReceiverId, partnerId)) ||
                    (m.SenderId == partnerId && string.Equals(m.ReceiverId, myId))
                )
                .OrderByDescending(m => m.SentAt)      
                .Skip(skip)                            
                .Take(take)                            
                .ToListAsync();

            messages.Reverse();
            return Ok(messages);
        }

        public class InboxItemDto
        {
            public string TargetId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Avatar { get; set; } = string.Empty;
            public string ChatType { get; set; } = string.Empty; // "direct" | "group"
            public ChatMessage? LastMessage { get; set; }
            public DateTime SortTime { get; set; }
        }

        [HttpGet("inbox")]
        public async Task<IActionResult> GetInbox()
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(myId)) return Unauthorized();

            // 1. Lấy danh sách các nhóm mà user tham gia (đã được duyệt)
            var myGroupIds = await _context.GroupMembers
                .Where(gm => gm.UserId == myId && gm.IsPendingApproval == false)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            // 2. Lấy tin nhắn cuối cùng của các cuộc chat 1-1
            var directInbox = await _context.ChatMessages
                .Where(m => (m.SenderId == myId && m.ReceiverId != null) || (m.ReceiverId == myId))
                .GroupBy(m => m.SenderId == myId ? m.ReceiverId : m.SenderId)
                .Select(g => new
                {
                    PartnerId = g.Key,
                    LastMessage = g.OrderByDescending(m => m.SentAt).FirstOrDefault()
                })
                .Where(x => x.LastMessage != null)
                .ToListAsync();

            // 3. Lấy tin nhắn cuối cùng của các nhóm chat
            var groupInbox = await _context.ChatMessages
                .Where(m => m.GroupName != null && myGroupIds.Contains(m.GroupName))
                .GroupBy(m => m.GroupName)
                .Select(g => new
                {
                    GroupId = g.Key,
                    LastMessage = g.OrderByDescending(m => m.SentAt).FirstOrDefault()
                })
                .Where(x => x.LastMessage != null)
                .ToListAsync();

            // 4. Lấy thông tin chi tiết của các User đối tác trong chat 1-1
            var partnerIds = directInbox.Select(x => x.PartnerId).ToList();
            var users = await _context.Users
                .Where(u => partnerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.AvatarUrl });

            // 5. Lấy thông tin chi tiết của các Nhóm chat (kể cả có tin nhắn hay chưa)
            var chatGroups = await _context.ChatGroups
                .Where(g => myGroupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => new { g.Name, g.AvatarUrl, g.CreatedAt });

            // 6. Xây dựng danh sách Inbox tổng hợp
            var result = new List<InboxItemDto>();

            // Thêm các cuộc chat 1-1
            foreach (var item in directInbox)
            {
                if (users.TryGetValue(item.PartnerId!, out var u))
                {
                    result.Add(new InboxItemDto
                    {
                        TargetId = item.PartnerId!,
                        Name = u.DisplayName,
                        Avatar = u.AvatarUrl,
                        ChatType = "direct",
                        LastMessage = item.LastMessage,
                        SortTime = item.LastMessage!.SentAt
                    });
                }
            }

            // Thêm các cuộc chat nhóm có tin nhắn
            var groupIdsWithMessages = groupInbox.Select(x => x.GroupId).ToHashSet();
            foreach (var item in groupInbox)
            {
                if (chatGroups.TryGetValue(item.GroupId!, out var g))
                {
                    result.Add(new InboxItemDto
                    {
                        TargetId = item.GroupId!,
                        Name = g.Name,
                        Avatar = g.AvatarUrl,
                        ChatType = "group",
                        LastMessage = item.LastMessage,
                        SortTime = item.LastMessage!.SentAt
                    });
                }
            }

            // Thêm các nhóm chưa có tin nhắn nào để user vẫn thấy phòng chat mới lập/mới join
            foreach (var gId in myGroupIds)
            {
                if (!groupIdsWithMessages.Contains(gId) && chatGroups.TryGetValue(gId, out var g))
                {
                    result.Add(new InboxItemDto
                    {
                        TargetId = gId,
                        Name = g.Name,
                        Avatar = g.AvatarUrl,
                        ChatType = "group",
                        LastMessage = null,
                        SortTime = g.CreatedAt
                    });
                }
            }

            // Sắp xếp toàn bộ Inbox theo thời gian mới nhất (tin nhắn mới nhất hoặc ngày tạo nhóm nếu chưa có tin)
            var sortedInbox = result
                .OrderByDescending(x => x.SortTime)
                .ToList();

            return Ok(sortedInbox);
        }

        [HttpGet("groups/{groupName}")]
        public async Task<IActionResult> GetGroupHistory(string groupName, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.GroupName == groupName)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .Select(m => new {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    GroupName = m.GroupName,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsRevoked = m.IsRevoked,
                    IsRead = m.IsRead,
                    SenderName = _context.Users.Where(u => u.Id == m.SenderId).Select(u => u.DisplayName).FirstOrDefault() ?? "Ai đó...",
                    SenderAvatar = _context.Users.Where(u => u.Id == m.SenderId).Select(u => u.AvatarUrl).FirstOrDefault() ?? ""
                })
                .ToListAsync();

            messages.Reverse();
            return Ok(messages);
        }
    }
}
