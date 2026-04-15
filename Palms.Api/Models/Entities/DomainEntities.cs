namespace Palms.Api.Models.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Uuid { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? District { get; set; }
        public int? AkcId { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? LastLoginIp { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Applicant
    {
        public int Id { get; set; }
        public string Uuid { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Application
    {
        public int Id { get; set; }
        public string Uuid { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public int ApplicantId { get; set; }
        public string ApplicationType { get; set; } = "NEW";
        public string? LicenseCategory { get; set; }
        public string Status { get; set; } = "SUBMITTED";
        public string FirmName { get; set; } = string.Empty;
        public string? RegistrationNumber { get; set; }
        public string? PanVatNumber { get; set; }
        public string? PriorLicenseNumber { get; set; }
        public DateTime? PriorLicenseExpiry { get; set; }
        public string AuthorizedPerson { get; set; } = string.Empty;
        public string? CitizenshipNumber { get; set; }
        public string? Designation { get; set; }
        public string? AddressGapaNapa { get; set; }
        public string? AddressWard { get; set; }
        public string AddressDistrict { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TrainingCertHolder { get; set; }
        public string? EducationalQualification { get; set; }
        public string? BusinessDescription { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal? PaymentAmount { get; set; }
        public bool PaymentConfirmed { get; set; }
        public int? PaymentConfirmedBy { get; set; }
        public DateTime? PaymentConfirmedAt { get; set; }
        public int? AssignedAkcId { get; set; }
        public bool IsLateSubmission { get; set; }
        public bool LateFeeApplicable { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? AkcReviewedAt { get; set; }
        public DateTime? RevenueCheckedAt { get; set; }
        public DateTime? PpoReviewedAt { get; set; }
        public DateTime? ChiefApprovedAt { get; set; }
        public DateTime? IssuedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class License
    {
        public int Id { get; set; }
        public string Uuid { get; set; } = string.Empty;
        public string LicenseNumber { get; set; } = string.Empty;
        public int ApplicationId { get; set; }
        public int ApplicantId { get; set; }
        public string FirmName { get; set; } = string.Empty;
        public string SellerName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string AddressDistrict { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public string? QrCodeData { get; set; }
        public string? PdfPath { get; set; }
        public int? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? SuspensionReason { get; set; }
        public DateTime? SuspensionDate { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? CancellationDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ApplicationDocument
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public string DocType { get; set; } = string.Empty;
        public string OriginalFilename { get; set; } = string.Empty;
        public string StoredFilename { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
