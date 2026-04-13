using ChatService.Api.Data;
using ChatService.Api.Hubs;
using ChatService.Api.Models;
using ChatService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatService.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GroupsController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ConnectionTracker _tracker;

        public GroupsController(ChatDbContext context, IHubContext<ChatHub> hubContext, ConnectionTracker tracker)
        {
            _context = context;
            _hubContext = hubContext;
            _tracker = tracker;
        }

        // 1. Tạo Nhóm (Admin)
        [HttpPost("create")]
        public async Task<IActionResult> CreateGroup([FromBody] string groupName)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = new ChatGroup
            {
                Name = string.IsNullOrWhiteSpace(groupName) ? "Nhóm Mới" : groupName,
                AdminId = myId!
            };

            _context.ChatGroups.Add(group);
            
            // Ép Trưởng phòng vào DB Member (Với tư cách đã Approved)
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = group.Id,
                UserId = myId!,
                IsPendingApproval = false
            });

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Đã Tạo Nhóm.", GroupId = group.Id, GroupName = group.Name });
        }

        // 2. Tìm danh sách TẤT CẢ các Nhóm MÌNH ĐẠT TIÊU CHUẨN ĐANG THAM GIA
        [HttpGet("my")]
        public async Task<IActionResult> GetMyGroups()
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var myGroupIds = await _context.GroupMembers
                .Where(gm => gm.UserId == myId && gm.IsPendingApproval == false)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            var groups = await _context.ChatGroups
                .Where(g => myGroupIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name, g.AvatarUrl, IsAdmin = g.AdminId == myId })
                .ToListAsync();

            return Ok(groups);
        }

        // 3. Tra cứu 1 Mã Nhóm bên ngoài đường (Để XIN VÀO)
        [HttpGet("search/{groupId}")]
        public async Task<IActionResult> InspectGroup(string groupId)
        {
            var group = await _context.ChatGroups.FindAsync(groupId);
            if (group == null) return NotFound("Mã Nhóm không Tồn Tại.");

            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var memberStatus = await _context.GroupMembers.FindAsync(groupId, myId);

            return Ok(new { 
                group.Id, group.Name, group.AvatarUrl, 
                JoinStatus = memberStatus == null ? -1 : (memberStatus.IsPendingApproval ? 0 : 1) 
            });
        }

        // 4. Xin Vào Nhóm (NẾU chưa có Trạng Thái Gì)
        [HttpPost("join/{groupId}")]
        public async Task<IActionResult> RequestJoinGroup(string groupId)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.ChatGroups.FindAsync(groupId);
            if(group == null) return NotFound();

            var member = new GroupMember { GroupId = groupId, UserId = myId!, IsPendingApproval = true };
            _context.GroupMembers.Add(member);
            await _context.SaveChangesAsync();

            // (Có thể) Réo chuông cho Admin của nhóm biết qua SignalR
            return Ok(new { Message = "Đã Nạp Đơn Xin Vào Nhóm. Vui lòng chờ Trưởng Phòng duyệt."});
        }

        // 5. API Dành Riêng Trưởng Phòng: Lấy DS Xin Vào Mới
        [HttpGet("{groupId}/requests")]
        public async Task<IActionResult> GetPendingJoinRequests(string groupId)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.ChatGroups.FindAsync(groupId);
            if (group == null || group.AdminId != myId) return Unauthorized("Chỉ Trưởng phòng mới được xem.");

            var pendings = await _context.GroupMembers
                .Where(gm => gm.GroupId == groupId && gm.IsPendingApproval == true)
                .Join(_context.Users, gm => gm.UserId, u => u.Id, (gm, u) => new { u.Id, u.DisplayName, u.AvatarUrl })
                .ToListAsync();
            return Ok(pendings);
        }

        // 6. Trưởng Phòng Duyệt Đội Viên
        [HttpPost("{groupId}/approve/{userId}")]
        public async Task<IActionResult> ApproveMember(string groupId, string userId)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = await _context.ChatGroups.FindAsync(groupId);
            if (group == null || group.AdminId != myId) return Unauthorized();

            var mem = await _context.GroupMembers.FindAsync(groupId, userId);
            if (mem == null) return NotFound();

            mem.IsPendingApproval = false;
            await _context.SaveChangesAsync();
            
            // Xong xuôi thì lôi đầu nó Nhét lại vào Không gian SignalR Nhóm!
            // (Đòi hỏi lúc User connect SignalR phải quét MyGroups để add nó vào sẵn từ đầu, hoặc chích thẳng ở đây nếu Đang Online)
            
            return Ok(new { Message = "Đã thu nhận thành viên!" });
        }
    }
}
