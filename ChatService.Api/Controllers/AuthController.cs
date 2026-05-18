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
                // Gá»i API cá»§a Google Ä‘á»ƒ tra cá»©u xem ClientID cÃ³ khá»›p khÃ´ng
                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new List<string> { _config["Google:ClientId"]! }
                };

                // Nhá» Google gá»¡ niÃªm phong cá»¥c Credential, moi ra Avatar, Email...
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential, settings);

                // DÃ¹ng Email lÃ m tháº» cÄƒn cÆ°á»›c tra DB mÃ¬nh xem tháº±ng nÃ y tá»«ng chat chÆ°a
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
                if (user == null)
                {
                    // Láº§n Ä‘áº§u vÃ o Web -> Láº­p "há»™ kháº©u" Zalo tá»± Ä‘á»™ng
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

                // Cáº¥p Tháº» BÃ i JWT "CÃ¢y nhÃ  lÃ¡ vÆ°á»n" Ä‘á»ƒ Ä‘i chÆ¡i kháº¯p ngÃµ ngÃ¡ch SignalR
                var token = GenerateJwtToken(user);

                return Ok(new
                {
                    token = token,
                    user = new { id = user.Id, name = user.DisplayName, avatar = user.AvatarUrl }
                });
            }
            catch (InvalidJwtException)
            {
                return BadRequest("MÃ£ Google bá»‹ gian láº­n hoáº·c láº­u kháº©u!");
            }
        }

        private string GenerateJwtToken(AppUser user)
        {
            // KhoÃ¡ nÃ y khá»›p y chang vá»›i cáº¥u hÃ¬nh bÃªn Program.cs
            var secretKey = _config["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    // Quan Trá»ng LÃµi: NhÃ©t ID Database vÃ o cá»™t sá»‘ng cá»§a Tháº» bÃ i
                    new Claim(ClaimTypes.NameIdentifier, user.Id), 
                    new Claim(ClaimTypes.Name, user.DisplayName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("avatar", user.AvatarUrl),
                    new Claim("sub", user.Id) // Fake sub cho SignalR cÅ© dá»… nháº­n diá»‡n
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}

