using Dapper;
using EduPlatform.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EduPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IDbConnection _db;

    public AuthController(IConfiguration configuration, IDbConnection db)
    {
        _configuration = configuration;
        _db = db;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        const string existsSql = "SELECT COUNT(1) FROM users WHERE username = @Username";
        var exists = await _db.ExecuteScalarAsync<int>(existsSql, new { request.Username });
        if (exists > 0)
        {
            return Conflict(new { message = "Пользователь с таким логином уже существует" });
        }

        const string insertSql = """
            INSERT INTO users (username, password_hash, role_id, created_at)
            VALUES (@Username, @Password, 2, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        var id = await _db.ExecuteScalarAsync<int>(insertSql, new
        {
            request.Username,
            request.Password
        });

        return Ok(new { id, username = request.Username, role = "Student" });
    }

    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] LoginRequest request)
    {
        const string sql = """
            SELECT u.id, u.username, u.password_hash, u.role_id, r.role_name
            FROM users u
            JOIN roles r ON r.id = u.role_id
            WHERE u.username = @Login
            """;

        var user = await _db.QueryFirstOrDefaultAsync<UserAuthRow>(sql, new { request.Login });

        if (user == null || request.Password != user.password_hash)
        {
            return Unauthorized(new { message = "Invalid login or password" });
        }

        var jwt = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.id.ToString()),
            new Claim(ClaimTypes.Name, user.username),
            new Claim(ClaimTypes.Role, user.role_name)
        };

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials
        );

        return Ok(new
        {
            access_token = new JwtSecurityTokenHandler().WriteToken(token),
            token_type = "Bearer",
            expires_in = 1800,
            role = user.role_name,
            user_id = user.id
        });
    }

    private sealed class UserAuthRow
    {
        public int id { get; set; }
        public string username { get; set; } = string.Empty;
        public string password_hash { get; set; } = string.Empty;
        public int role_id { get; set; }
        public string role_name { get; set; } = string.Empty;
    }
}

