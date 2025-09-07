using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hobow_Server.Services;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace Hobow_Server.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;

    public AuthenticationController(IAuthenticationService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public IActionResult Register(AuthenticationRequest request)
    {
        var (success, content) = _authService.Register(request.Username, request.Password);
        if (!success)
        {
            return BadRequest(content);
        }
        return Login(request);
    }


    [HttpPost("login")]
    public IActionResult Login(AuthenticationRequest request)
    {
        var (success, content) = _authService.Login(request.Username, request.Password);
        if (!success)
        {
            return BadRequest(content);
        }
        return Ok(new AuthenticationResponse()
        {
            Token = content
        });
    }

    [HttpGet("session-status")]
    [Authorize]
    public IActionResult GetSessionStatus()
    {
        try
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            var sessionIdClaim = User.FindFirst("sessionId")?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(sessionIdClaim))
                return Unauthorized(new { Error = "User not authenticated" });

            // Session đã được validate bởi middleware
            return Ok(new { 
                IsOnline = true,
                Message = "Session is active"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        try
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { Error = "User not authenticated" });

            var userId = int.Parse(userIdClaim);
            
            // CLEAR SESSION TRONG DATABASE
            var result = _authService.Logout(userId);
            
            if (result.success)
            {
                return Ok(new { Message = "Logged out successfully" });
            }
            else
            {
                return BadRequest(new { Error = result.content });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }
}