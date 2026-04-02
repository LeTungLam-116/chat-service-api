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
    public class UsersController : ControllerBase
    {
        private readonly ChatDbContext _context;

        public UsersController(ChatDbContext context)
        {
            _context = context;
        }

        // TÍNH NĂNG TÌM KIẾM DANH BẠ
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string q)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Bác ấn thanh Search mà gõ rỗng, hệ thống Lôi đại 5 người online gần nhất vứt lên gợi ý!
            if (string.IsNullOrWhiteSpace(q))
            {
                var randomRecent = await _context.Users
                    .Where(u => u.Id != myId)
                    .OrderByDescending(u => u.LastOnlineAt)
                    .Take(5)
                    .Select(u => new { id = u.Id, name = u.DisplayName, avatar = u.AvatarUrl })
                    .ToListAsync();
                return Ok(randomRecent);
            }

            // Gõ có chữ thì quét Tên Zalo hoặc Email
            var users = await _context.Users
                .Where(u => u.Id != myId && (u.DisplayName.Contains(q) || u.Email.Contains(q)))
                .Take(10)
                .Select(u => new { id = u.Id, name = u.DisplayName, avatar = u.AvatarUrl })
                .ToListAsync();

            return Ok(users);
        }
    }
}
