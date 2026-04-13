using System;

namespace ChatService.Api.Models
{
    public class GroupMember
    {
        public string GroupId { get; set; } = string.Empty;
        
        public string UserId { get; set; } = string.Empty;
        
        // Trạng thái: Cứ múa gõ Xin Vô Nhóm là bay vào, nhưng bị ghim cờ Pending. Đợi Trưởng phòng gật đầu thì mới là Thành viên xịn!
        public bool IsPendingApproval { get; set; } = false; 
        
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
