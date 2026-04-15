using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palms.Api.Models.DTOs;
using Palms.Api.Services;

namespace Palms.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { Error = "Username and password are required." });

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var (response, error) = await _authService.AuthenticateStaffAsync(request.Username, request.Password, ipAddress);

            if (error != null)
                return Unauthorized(new { Error = error });

            return Ok(response);
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var user = new
            {
                Id = User.FindFirst("id")?.Value,
                Username = User.FindFirst("username")?.Value,
                Role = User.FindFirst("role")?.Value,
                AkcId = User.FindFirst("akc_id")?.Value,
                District = User.FindFirst("district")?.Value
            };

            return Ok(user);
        }
    }
}
