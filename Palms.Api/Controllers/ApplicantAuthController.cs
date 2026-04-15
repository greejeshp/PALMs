using Microsoft.AspNetCore.Mvc;
using Palms.Api.Models.DTOs;
using Palms.Api.Models.Entities;
using Palms.Api.Repositories;
using Palms.Api.Services;
using System.Security.Cryptography;

namespace Palms.Api.Controllers
{
    [ApiController]
    [Route("api/applicant/auth")]
    public class ApplicantAuthController : ControllerBase
    {
        private readonly IApplicantRepository _applicantRepo;
        private readonly OtpService _otpService;
        private readonly AuthService _authService;

        public ApplicantAuthController(IApplicantRepository applicantRepo, OtpService otpService, AuthService authService)
        {
            _applicantRepo = applicantRepo;
            _otpService = otpService;
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] ApplicantRegisterDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Mobile) || req.Mobile.Length != 10)
                return BadRequest(new { Error = "Valid 10-digit mobile number required." });

            var existing = await _applicantRepo.GetByMobileAsync(req.Mobile);
            if (existing != null)
                return BadRequest(new { Error = "Mobile number is already registered." });

            var app = new Applicant
            {
                Mobile = req.Mobile,
                FullName = req.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, 10),
                IsVerified = false,
                IsActive = true
            };

            var newId = await _applicantRepo.CreateApplicantAsync(app);
            
            // Dispatch OTP
            var devOtp = await _otpService.GenerateAndSendOtpAsync(newId, req.Mobile);

            return Ok(new { 
                Message = "Registration successful. OTP sent.", 
                ApplicantId = newId,
                DevOtp = devOtp // REMOVE IN PRODUCTION
            });
        }

        // Login supports mobile OR email
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApplicantLoginDto req)
        {
            var (response, error) = await _authService.AuthenticateApplicantAsync(req.Mobile, req.Password);
            if (error != null) return BadRequest(new { Error = error });

            return Ok(response);
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto req)
        {
            var applicant = await _applicantRepo.GetByMobileAsync(req.Mobile);
            if (applicant == null) return NotFound(new { Error = "Applicant not found." });

            var (isValid, error) = await _otpService.VerifyOtpAsync(req.Mobile, req.OtpCode);
            if (!isValid) return BadRequest(new { Error = error });

            await _applicantRepo.UpdateVerificationStatusAsync(applicant.Id, true);

            var profile = await _applicantRepo.GetApplicantProfileAsync(applicant.Id);
            var token = _authService.GenerateApplicantToken(applicant, profile);

            return Ok(new ApplicantAuthResponseDto { 
                Token = token, 
                Profile = profile ?? new ApplicantProfileDto { Id = applicant.Id, Mobile = applicant.Mobile, FullName = applicant.FullName }
            });
        }

        // ─── FORGOT PASSWORD ──────────────────────────────────────────────────────
        // Step 1: Request OTP via email or mobile
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Identifier))
                return BadRequest(new { Error = "Email or mobile number is required." });

            // Look up by email or mobile
            var applicant = req.Identifier.Contains('@')
                ? await _applicantRepo.GetByEmailAsync(req.Identifier)
                : await _applicantRepo.GetByMobileAsync(req.Identifier);

            if (applicant == null || !applicant.IsActive)
                return BadRequest(new { Error = "No account found with this email or mobile." });

            // Generate OTP
            string otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            string otpHash = BCrypt.Net.BCrypt.HashPassword(otp, 10);
            var expiry = DateTime.UtcNow.AddMinutes(10);

            await _applicantRepo.SaveOtpAsync(applicant.Id, applicant.Mobile, otpHash, expiry, "RESET");

            // Simulate email/SMS
            var email = applicant.Email ?? "(no email on file)";
            Console.WriteLine($"[EMAIL GATEWAY] Password Reset OTP: {otp} → sent to {email} / {applicant.Mobile}");

            return Ok(new {
                Message = "OTP sent to your registered email and mobile.",
                Mobile = MaskMobile(applicant.Mobile),
                Email = MaskEmail(email),
                DevOtp = otp // REMOVE IN PRODUCTION
            });
        }

        // Step 2: Verify reset OTP and set new password
        [HttpPost("reset-password-otp")]
        public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordOtpDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Identifier) || string.IsNullOrWhiteSpace(req.OtpCode) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { Error = "Identifier, OTP, and new password are required." });

            if (req.NewPassword.Length < 8)
                return BadRequest(new { Error = "Password must be at least 8 characters." });

            var applicant = req.Identifier.Contains('@')
                ? await _applicantRepo.GetByEmailAsync(req.Identifier)
                : await _applicantRepo.GetByMobileAsync(req.Identifier);

            if (applicant == null) return BadRequest(new { Error = "Account not found." });

            var record = await _applicantRepo.GetLatestOtpByPurposeAsync(applicant.Mobile, "RESET");
            if (record == null) return BadRequest(new { Error = "No reset OTP found. Request a new one." });
            if ((bool)record.IsUsed) return BadRequest(new { Error = "OTP already used." });
            if (DateTime.UtcNow > (DateTime)record.ExpiresAt) return BadRequest(new { Error = "OTP has expired." });
            if (!BCrypt.Net.BCrypt.Verify(req.OtpCode, (string)record.OtpHash)) return BadRequest(new { Error = "Invalid OTP." });

            await _applicantRepo.MarkOtpAsUsedAsync((int)record.Id);

            var newHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, 10);
            await _applicantRepo.ResetPasswordAsync(applicant.Id, newHash);

            return Ok(new { Message = "Password reset successfully. You can now log in." });
        }

        // ─── OTP LOGIN (passwordless) ─────────────────────────────────────────────
        // Step 1: Request login OTP
        [HttpPost("request-otp-login")]
        public async Task<IActionResult> RequestOtpLogin([FromBody] ForgotPasswordDto req)
        {
            var applicant = req.Identifier.Contains('@')
                ? await _applicantRepo.GetByEmailAsync(req.Identifier)
                : await _applicantRepo.GetByMobileAsync(req.Identifier);

            if (applicant == null || !applicant.IsActive)
                return BadRequest(new { Error = "No account found." });

            string otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            string otpHash = BCrypt.Net.BCrypt.HashPassword(otp, 10);
            await _applicantRepo.SaveOtpAsync(applicant.Id, applicant.Mobile, otpHash, DateTime.UtcNow.AddMinutes(5), "LOGIN");

            var email = applicant.Email ?? "(no email)";
            Console.WriteLine($"[EMAIL GATEWAY] Login OTP: {otp} → {email} / {applicant.Mobile}");

            return Ok(new {
                Message = "Login OTP sent.",
                Mobile = MaskMobile(applicant.Mobile),
                Email = MaskEmail(email),
                DevOtp = otp
            });
        }

        // Step 2: Login with OTP
        [HttpPost("login-with-otp")]
        public async Task<IActionResult> LoginWithOtp([FromBody] LoginWithOtpDto req)
        {
            var applicant = req.Identifier.Contains('@')
                ? await _applicantRepo.GetByEmailAsync(req.Identifier)
                : await _applicantRepo.GetByMobileAsync(req.Identifier);

            if (applicant == null || !applicant.IsActive)
                return BadRequest(new { Error = "Account not found." });

            var record = await _applicantRepo.GetLatestOtpByPurposeAsync(applicant.Mobile, "LOGIN");
            if (record == null) return BadRequest(new { Error = "No login OTP found." });
            if ((bool)record.IsUsed) return BadRequest(new { Error = "OTP already used." });
            if (DateTime.UtcNow > (DateTime)record.ExpiresAt) return BadRequest(new { Error = "OTP expired." });
            if (!BCrypt.Net.BCrypt.Verify(req.OtpCode, (string)record.OtpHash)) return BadRequest(new { Error = "Invalid OTP." });

            await _applicantRepo.MarkOtpAsUsedAsync((int)record.Id);

            // Auto-verify if not verified yet
            if (!applicant.IsVerified)
                await _applicantRepo.UpdateVerificationStatusAsync(applicant.Id, true);

            var profile = await _applicantRepo.GetApplicantProfileAsync(applicant.Id);
            var token = _authService.GenerateApplicantToken(applicant, profile);

            return Ok(new ApplicantAuthResponseDto {
                Token = token,
                Profile = profile ?? new ApplicantProfileDto { Id = applicant.Id, Mobile = applicant.Mobile, FullName = applicant.FullName }
            });
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private static string MaskMobile(string mobile) =>
            mobile.Length >= 6 ? mobile[..3] + "****" + mobile[^3..] : "***";

        private static string MaskEmail(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 0) return email;
            return email[..Math.Min(2, at)] + "****" + email[at..];
        }
    }

    public record ForgotPasswordDto(string Identifier);
    public record ResetPasswordOtpDto(string Identifier, string OtpCode, string NewPassword);
    public record LoginWithOtpDto(string Identifier, string OtpCode);
}
