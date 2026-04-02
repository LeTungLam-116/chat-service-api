using System;
using System.ComponentModel.DataAnnotations;

namespace ChatService.Api.Models
{
    public class ChatMessage
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        // ID of the user sending the message (maps to Identity Auth DB)
        [Required]
        public string SenderId { get; set; } = string.Empty; 
        
        // For Direct 1-on-1 Chat
        public string? ReceiverId { get; set; } 
        
        // For Group/Room Chat
        public string? GroupName { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;
        
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        
        // Cờ đánh dấu lúc người gửi chọn "Thu Hồi Tin Nhắn"
        public bool IsRevoked { get; set; } = false;
    }
}
