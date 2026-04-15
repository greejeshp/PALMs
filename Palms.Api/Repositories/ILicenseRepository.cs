using Palms.Api.Models.Entities;

namespace Palms.Api.Repositories
{
    public interface ILicenseRepository
    {
        Task<int> CreateLicenseAsync(License license);
        Task<License?> GetByLicenseNumberAsync(string licenseNumber);
        Task<Palms.Api.Models.DTOs.LicenseDetailDto?> GetLicenseDetailByNumberAsync(string licenseNumber);
        Task<IEnumerable<License>> GetAllLicensesAsync();
        Task<IEnumerable<License>> GetLicensesByApplicantIdAsync(int applicantId);
        Task UpdatePdfPathAsync(int id, string pdfPath);
        Task UpdateLicenseRenewalAsync(License license);
    }
}
