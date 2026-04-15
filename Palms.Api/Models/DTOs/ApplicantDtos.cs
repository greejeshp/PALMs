namespace Palms.Api.Models.DTOs
{
    public class ApplicantRegisterDto
    {
        public string Mobile { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class VerifyOtpDto
    {
        public string Mobile { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
    }

    public class ApplicantLoginDto
    {
        public string Mobile { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ApplicantAuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public ApplicantProfileDto Profile { get; set; } = null!;
    }

    public class ApplicantProfileDto
    {
        public int Id { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        
        // Profile Info
        public string? FirmName { get; set; }
        public string? AddressDistrict { get; set; }
        public bool ProfileComplete { get; set; }
    }

    public class ProfileUpdateRequest
    {
        public string Type { get; set; } = string.Empty; // MOBILE or EMAIL
        public string NewValue { get; set; } = string.Empty;
    }

    public class ProfileUpdateVerifyRequest
    {
        public string Type { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
    }
}
