using Palms.Api.Models.Entities;
using Palms.Api.Models.DTOs;

namespace Palms.Api.Repositories
{
    public interface IApplicantRepository
    {
        Task<Applicant?> GetByMobileAsync(string mobile);
        Task<Applicant?> GetByEmailAsync(string email);
        Task<Applicant?> GetByMobileOrEmailAsync(string identifier);
        Task<Applicant?> GetByIdAsync(int id);
        Task<int> CreateApplicantAsync(Applicant applicant);
        Task UpdateVerificationStatusAsync(int id, bool isVerified);
        
        Task SaveOtpAsync(int applicantId, string mobile, string otpHash, DateTime expiresAt, string purpose = "REGISTRATION");
        Task<dynamic?> GetLatestOtpAsync(string mobile);
        Task<dynamic?> GetLatestOtpByPurposeAsync(string mobile, string purpose);
        Task MarkOtpAsUsedAsync(int otpId);
        Task<Palms.Api.Models.DTOs.ApplicantProfileDto?> GetApplicantProfileAsync(int applicantId);
        Task UpdateEmailAsync(int id, string email);
        Task UpdateMobileAsync(int id, string mobile);
        Task ResetPasswordAsync(int id, string newHash);
    }
}
