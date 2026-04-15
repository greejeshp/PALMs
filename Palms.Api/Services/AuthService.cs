using Microsoft.IdentityModel.Tokens;
using Palms.Api.Models.DTOs;
using Palms.Api.Models.Entities;
using Palms.Api.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace Palms.Api.Services
{
    public class AuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IApplicantRepository _applicantRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IApplicantRepository applicantRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _applicantRepository = applicantRepository;
            _configuration = configuration;
        }

        public async Task<(AuthResponseDto?, string?)> AuthenticateStaffAsync(string username, string password, string ipAddress)
        {
            var user = await _userRepository.GetUserByUsernameAsync(username);

            if (user == null || !user.IsActive)
                return (null, "Invalid username or account is disabled.");

            if (user.IsLocked)
                return (null, "Account is locked due to too many failed login attempts.");

            // Verify password using BCrypt
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            if (!isPasswordValid)
            {
                await _userRepository.IncrementFailedLoginAsync(user.Id);
                return (null, "Invalid password.");
            }

            // Success
            await _userRepository.UpdateLastLoginAsync(user.Id, ipAddress);

            var token = GenerateJwtToken(user);

            var response = new AuthResponseDto
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FullName = user.FullName,
                    Role = user.Role,
                    District = user.District,
                    AkcId = user.AkcId
                }
            };

            return (response, null);
        }

        public async Task<(ApplicantAuthResponseDto?, string?)> AuthenticateApplicantAsync(string identifier, string password)
        {
            // Support login by mobile OR email
            var applicant = identifier.Contains('@')
                ? await _applicantRepository.GetByEmailAsync(identifier)
                : await _applicantRepository.GetByMobileAsync(identifier);

            if (applicant == null || !applicant.IsActive)
                return (null, "Invalid credentials or account is disabled.");

            if (!applicant.IsVerified)
                return (null, "Account is not verified. Please verify your mobile number first.");

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, applicant.PasswordHash);

            if (!isPasswordValid)
                return (null, "Invalid password.");

            var profile = await _applicantRepository.GetApplicantProfileAsync(applicant.Id);
            var token = GenerateApplicantToken(applicant, profile);

            var response = new ApplicantAuthResponseDto
            {
                Token = token,
                Profile = profile ?? new ApplicantProfileDto { Id = applicant.Id, Mobile = applicant.Mobile, FullName = applicant.FullName }
            };

            return (response, null);
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var keyBytes = Encoding.ASCII.GetBytes(jwtSettings["Secret"]!);
            var expiryMinutes = Convert.ToInt32(jwtSettings["ExpiryMinutes"]);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("id", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("username", user.Username)
            };

            if (user.AkcId.HasValue)
            {
                claims.Add(new Claim("akc_id", user.AkcId.Value.ToString()));
            }
            if (!string.IsNullOrEmpty(user.District))
            {
                claims.Add(new Claim("district", user.District));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(keyBytes), 
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public string GenerateApplicantToken(Applicant app, ApplicantProfileDto? profile)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var keyBytes = Encoding.ASCII.GetBytes(jwtSettings["Secret"]!);
            var expiryMinutes = Convert.ToInt32(jwtSettings["ExpiryMinutes"]);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, app.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("id", app.Id.ToString()),
                new Claim(ClaimTypes.Role, "APPLICANT"),
                new Claim("mobile", app.Mobile)
            };

            // Applicants don't have AKC or District until they apply, but if they do, we add them
            if (profile != null)
            {
                if (!string.IsNullOrEmpty(profile.AddressDistrict))
                    claims.Add(new Claim("district", profile.AddressDistrict));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(keyBytes), 
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
