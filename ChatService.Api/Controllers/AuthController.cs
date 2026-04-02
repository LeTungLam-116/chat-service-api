using ChatService.Api.Data;
using ChatService.Api.Models;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ChatService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(ChatDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public class GoogleLoginRequest
        {
            public string Credential { get; set; } = string.Empty;
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                // Gọi API của Google để tra cứu xem ClientID có khớp không
                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new List<string> { _config["Google:ClientId"]! }
                };

                // Nhờ Google gỡ niêm phong cục Credential, moi ra Avatar, Email...
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential, settings);

                // Dùng Email làm thẻ căn cước tra DB mình xem thằng này từng chat chưa
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
                if (user == null)
                {
                    // Lần đầu vào Web -> Lập "hộ khẩu" Zalo tự động
                    user = new AppUser
                    {
                        GoogleId = payload.Subject,
                        Email = payload.Email,
                        DisplayName = payload.Name,
                        AvatarUrl = payload.Picture,
                        LastOnlineAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                // Cấp Thẻ Bài JWT "Cây nhà lá vườn" để đi chơi khắp ngõ ngách SignalR
                var token = GenerateJwtToken(user);

                return Ok(new
                {
                    token = token,
                    user = new { id = user.Id, name = user.DisplayName, avatar = user.AvatarUrl }
                });
            }
            catch (InvalidJwtException)
            {
                return BadRequest("Mã Google bị gian lận hoặc lậu khẩu!");
            }
        }

        private string GenerateJwtToken(AppUser user)
        {
            // Khoá này khớp y chang với cấu hình bên Program.cs
            var secretKey = "ielts-selfstudy-super-secret-key-that-must-be-long";
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    // Quan Trọng Lõi: Nhét ID Database vào cột sống của Thẻ bài
                    new Claim(ClaimTypes.NameIdentifier, user.Id), 
                    new Claim(ClaimTypes.Name, user.DisplayName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("avatar", user.AvatarUrl),
                    new Claim("sub", user.Id) // Fake sub cho SignalR cũ dễ nhận diện
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
