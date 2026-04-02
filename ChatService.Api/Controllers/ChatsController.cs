using ChatService.Api.Data;
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

        [HttpGet("inbox")]
        public async Task<IActionResult> GetInbox()
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Bất chấp giới hạn của EF Core GroupBy, ta nặn ra thuật toán lấy thẳng Object User kèm Tin Nhắn Cuối Cùng!
            var inbox = await _context.Users
                .Where(u => u.Id != myId && _context.ChatMessages.Any(m => 
                    (m.SenderId == myId && m.ReceiverId == u.Id) || 
                    (m.SenderId == u.Id && m.ReceiverId == myId)))
                .Select(u => new
                {
                    TargetId = u.Id,
                    Name = u.DisplayName,
                    Avatar = u.AvatarUrl,
                    LastMessage = _context.ChatMessages
                        .Where(m => (m.SenderId == myId && m.ReceiverId == u.Id) || (m.SenderId == u.Id && m.ReceiverId == myId))
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefault()
                })
                .OrderByDescending(x => x.LastMessage!.SentAt) // Đứa nào nhắn gần nhất nổi lên đầu
                .ToListAsync();

            return Ok(inbox);
        }

        [HttpGet("groups/{groupName}")]
        public async Task<IActionResult> GetGroupHistory(string groupName, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.GroupName == groupName)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            messages.Reverse();
            return Ok(messages);
        }
    }
}
