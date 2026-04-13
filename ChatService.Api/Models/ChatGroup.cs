using System;
using System.ComponentModel.DataAnnotations;

namespace ChatService.Api.Models
{
    public class ChatGroup
    {
        // Ta xài 1 mã ngắn 8 ký tự làm ID kiêm Mã Mời Nhóm luôn (vd: 3A7K92NM) cho tiện xài như Zalo Web
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(); 
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        // ID kẻ lập phòng (Trưởng phòng) - Nắm mọi quyền sinh sát
        [Required]
        public string AdminId { get; set; } = string.Empty; 
        
        public string AvatarUrl { get; set; } = "https://cdn-icons-png.flaticon.com/512/615/615075.png";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
