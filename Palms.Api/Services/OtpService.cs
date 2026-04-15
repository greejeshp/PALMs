using Palms.Api.Repositories;
using System.Security.Cryptography;

namespace Palms.Api.Services
{
    public class OtpService
    {
        private readonly IApplicantRepository _applicantRepo;

        public OtpService(IApplicantRepository applicantRepo)
        {
            _applicantRepo = applicantRepo;
        }

        public async Task<string> GenerateAndSendOtpAsync(int applicantId, string mobile, string purpose = "REGISTRATION")
        {
            // 1. Generate 6-digit OTP
            string otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            
            // 2. Hash it
            string otpHash = BCrypt.Net.BCrypt.HashPassword(otp, 10);
            
            // 3. Save to database
            var expiry = DateTime.UtcNow.AddMinutes(10);
            await _applicantRepo.SaveOtpAsync(applicantId, mobile, otpHash, expiry, purpose);
            
            // 4. Simulate SMS Sending
            Console.WriteLine($"[{purpose}] [SMS GATEWAY] Sent OTP {otp} to {mobile}");
            
            return otp; // In production, DO NOT return the OTP to frontend.
        }

        public async Task<(bool isValid, string? error)> VerifyOtpAsync(string mobile, string otpCode, string purpose = "REGISTRATION")
        {
            var record = await _applicantRepo.GetLatestOtpByPurposeAsync(mobile, purpose);
            
            if (record == null)
                return (false, "No OTP found for this number.");
                
            if (record.IsUsed)
                return (false, "OTP has already been used.");
                
            if (DateTime.UtcNow > record.ExpiresAt)
                return (false, "OTP has expired.");
                
            bool isValid = BCrypt.Net.BCrypt.Verify(otpCode, record.OtpHash);
            
            if (!isValid)
                return (false, "Invalid OTP.");
                
            await _applicantRepo.MarkOtpAsUsedAsync(record.Id);
            return (true, null);
        }
    }
}
