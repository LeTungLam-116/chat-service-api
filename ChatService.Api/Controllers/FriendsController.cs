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
    public class FriendsController : ControllerBase
    {
        private readonly ChatDbContext _context;

        public FriendsController(ChatDbContext context)
        {
            _context = context;
        }

        // 1. Gửi Lời Mời Kết Bạn
        [HttpPost("request/{receiverId}")]
        public async Task<IActionResult> SendFriendRequest(string receiverId)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (myId == receiverId) return BadRequest("Không thể tự kết bạn với chính mình.");

            var existing = await _context.Friendships.FirstOrDefaultAsync(f => 
                (f.SenderId == myId && f.ReceiverId == receiverId) || 
                (f.SenderId == receiverId && f.ReceiverId == myId));

            if (existing != null) return BadRequest("Đã tồn tại trạng thái kết bạn.");

            var friendship = new Friendship
            {
                SenderId = myId!,
                ReceiverId = receiverId,
                Status = 0 // 0 = Chờ Duyệt (Pending)
            };

            _context.Friendships.Add(friendship);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Đã gửi lời mời.", FriendshipId = friendship.Id });
        }

        // 2. Đồng Ý Kết Bạn
        [HttpPost("accept/{friendshipId}")]
        public async Task<IActionResult> AcceptFriendRequest(int friendshipId)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var friendship = await _context.Friendships.FindAsync(friendshipId);

            if (friendship == null) return NotFound("Không tìm thấy lời mời.");
            if (friendship.ReceiverId != myId) return Unauthorized("Chỉ người nhận mới có quyền đồng ý.");

            friendship.Status = 1; // 1 = Thành Bạn Bè (Accepted)
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Trở thành bạn bè thành công." });
        }

        // 3. Xoá Bạn / Huỷ Lời Mời
        [HttpDelete("{friendshipId}")]
        public async Task<IActionResult> DeleteFriendship(int friendshipId)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var friendship = await _context.Friendships.FindAsync(friendshipId);

            if (friendship == null) return NotFound();
            if (friendship.SenderId != myId && friendship.ReceiverId != myId) return Unauthorized();

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Đã tuỷ hẹp quan hệ." });
        }
        
        // 4. Lấy List Lời Mời Đang Treo (Được Người Ta Mời)
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var requests = await _context.Friendships
                .Where(f => f.Status == 0 && f.ReceiverId == myId)
                .Select(f => new {
                    FriendshipId = f.Id,
                    SenderId = f.SenderId,
                    SenderName = _context.Users.Where(u => u.Id == f.SenderId).Select(u => u.DisplayName).FirstOrDefault(),
                    SenderAvatar = _context.Users.Where(u => u.Id == f.SenderId).Select(u => u.AvatarUrl).FirstOrDefault()
                }).ToListAsync();

            return Ok(requests);
        }

        // 5. Lấy Danh Sách Bạn Bè (Accepted)
        [HttpGet("")]
        public async Task<IActionResult> GetFriends()
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var friends = await _context.Friendships
                .Where(f => f.Status == 1 && (f.SenderId == myId || f.ReceiverId == myId))
                .Select(f => new {
                    FriendshipId = f.Id,
                    FriendId = f.SenderId == myId ? f.ReceiverId : f.SenderId
                }).ToListAsync();

            var friendIds = friends.Select(f => f.FriendId).ToList();
            var users = await _context.Users.Where(u => friendIds.Contains(u.Id)).ToListAsync();

            var result = friends.Select(f => {
                var u = users.First(x => x.Id == f.FriendId);
                return new {
                    Id = u.Id,
                    Name = u.DisplayName,
                    Avatar = u.AvatarUrl,
                    FriendshipId = f.FriendshipId
                };
            });

            return Ok(result);
        }
    }
}
