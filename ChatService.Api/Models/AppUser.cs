using System.ComponentModel.DataAnnotations;

namespace ChatService.Api.Models
{
    public class AppUser
    {
        [Required]
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string GoogleId { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public DateTime? LastOnlineAt { get; set; }
    }
}
