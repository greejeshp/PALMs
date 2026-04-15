namespace Palms.Api.Models.DTOs
{
    public class ApplicationSubmitDto
    {
        public string ApplicationType { get; set; } = "NEW";
        public string? LicenseCategory { get; set; }
        public string FirmName { get; set; } = string.Empty;
        public string? RegistrationNumber { get; set; }
        public string? PanVatNumber { get; set; }
        public string AuthorizedPerson { get; set; } = string.Empty;
        public string? CitizenshipNumber { get; set; }
        public string AddressDistrict { get; set; } = string.Empty;
        public string? AddressGapaNapa { get; set; }
        public string? Phone { get; set; }
        public string? BusinessDescription { get; set; }
    }

    public class ApplicationStatusDto
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FirmName { get; set; } = string.Empty;
        public string ApplicationType { get; set; } = string.Empty;
        public string? LicenseCategory { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? LicenseNumber { get; set; }
        public string? PdfPath { get; set; }
    }

    public class LicenseDetailDto
    {
        public string LicenseNumber { get; set; } = string.Empty;
        public string LicenseCategory { get; set; } = string.Empty;
        public string FirmName { get; set; } = string.Empty;
        public string? RegistrationNumber { get; set; }
        public string? PanVatNumber { get; set; }
        public string AuthorizedPerson { get; set; } = string.Empty;
        public string? CitizenshipNumber { get; set; }
        public string? Designation { get; set; }
        public string? AddressDistrict { get; set; }
        public string? AddressGapaNapa { get; set; }
        public string? AddressWard { get; set; }
        public string? Phone { get; set; }
        public string? BusinessDescription { get; set; }
        public string? ExpiryDate { get; set; }
    }
}
