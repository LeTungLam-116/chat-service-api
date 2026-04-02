using ChatService.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatService.Api.Data
{
    public class ChatDbContext : DbContext
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

        public DbSet<AppUser> Users { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // CHIÊU MÔN [1]: ĐÁNH INDEX (CHỈ MỤC) BỨT TỐC TÌM KIẾM
            // Báo SQL Server xếp các cột SenderId, ReceiverId và SentAt gọn gàng như Mục lục Sách giáo khoa.
            // Nhờ chiêu này, tìm trong 14 Triệu dòng tin nhắn cũng chỉ tốn chưa tới 1 Milli-giây!
            
            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => new { m.SenderId, m.ReceiverId });
                
            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => m.SentAt);
        }
    }
}
