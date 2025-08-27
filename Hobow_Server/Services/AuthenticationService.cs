using Hobow_Server.Models;  
using SharedLibrary;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;


namespace Hobow_Server.Services;

public class AuthenticationService : IAuthenticationService
{

    private readonly Settings _settings;
    private readonly GameDbContext _context;

    public AuthenticationService(Settings settings, GameDbContext context)
    {
        _settings = settings;
        _context = context;
    }

    public (bool success, string content) Register(string username, string password)
    {
        if (_context.Users.Any(u => u.Username == username)) return (false, "Invalid username");

        var hasher = new PasswordHasher<User>();
        var user = new User { Username = username };
        user.PasswordHash = hasher.HashPassword(user, password);

        _context.Add(user);
        _context.SaveChanges();

        return (true, "User registered successfully");
    }

    public (bool success, string content) Login(string username, string password)
    {
        var user = _context.Users.FirstOrDefault(u => u.Username == username);
        if (user == null) return (false, "Invalid username or password!");

        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Success)
        {
            return (true, GenerateJwtToken(AssembleClaimsIdentity(user)));
        }

        return (false, "Invalid username or password!");
    }

    private ClaimsIdentity AssembleClaimsIdentity(User user)
    {
        var subject = new ClaimsIdentity(new[]
        {
            new Claim("id", user.Id.ToString()),
        });

        return subject;
    }

    private string GenerateJwtToken(ClaimsIdentity subject)
    {  
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_settings.BearerKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = subject,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

public interface IAuthenticationService
{
    (bool success, string content) Register(string username, string password);
    (bool success, string content) Login(string username, string password);
}

