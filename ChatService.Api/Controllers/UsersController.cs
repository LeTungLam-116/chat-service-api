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

            // Quét Tên Zalo hoặc Email, kèm theo trạng thái móc nối với Bảng Kết Bạn
            var rawUsers = await _context.Users
                .Where(u => u.Id != myId && (string.IsNullOrWhiteSpace(q) || u.DisplayName.Contains(q) || u.Email.Contains(q)))
                .Take(10)
                .ToListAsync();

            // Móc với Friendship DB để biết chúng nó đã Kết Bạn chưa hay Đang Đợi
            var results = new List<object>();
            foreach (var u in rawUsers)
            {
                var f = await _context.Friendships.FirstOrDefaultAsync(x => 
                    (x.SenderId == myId && x.ReceiverId == u.Id) || 
                    (x.SenderId == u.Id && x.ReceiverId == myId));

                results.Add(new {
                    id = u.Id,
                    name = u.DisplayName,
                    avatar = u.AvatarUrl,
                    friendshipId = f?.Id ?? -1,
                    friendStatus = f?.Status ?? -1, // -1: Người Lạ, 0: Chờ Duyệt, 1: Bạn Bè
                    isSender = f?.SenderId == myId
                });
            }

            return Ok(results);
        }
    }
}
