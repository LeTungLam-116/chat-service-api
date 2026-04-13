using System;
using System.ComponentModel.DataAnnotations;

namespace ChatService.Api.Models
{
    public class Friendship
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string SenderId { get; set; } = string.Empty;
        
        [Required]
        public string ReceiverId { get; set; } = string.Empty;
        
        // 0: Đang chờ Đối phương duyệt (Pending), 1: Đã ưng thuận (Accepted)
        public int Status { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
