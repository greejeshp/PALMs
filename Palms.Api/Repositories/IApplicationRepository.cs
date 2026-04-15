using Palms.Api.Models.DTOs;
using Palms.Api.Models.Entities;

namespace Palms.Api.Repositories
{
    public interface IApplicationRepository
    {
        Task<int> CreateApplicationAsync(Application app);
        Task<Application?> GetApplicationByRefAsync(string referenceNumber);
        Task<Application?> GetApplicationByIdAsync(int id);
        Task<IEnumerable<ApplicationStatusDto>> GetApplicationsForApplicantAsync(int applicantId);
        
        // Staff queries
        Task<IEnumerable<ApplicationStatusDto>> GetApplicationsByAkcAndStatusAsync(int akcId, string status);
        Task<IEnumerable<ApplicationStatusDto>> GetApplicationsByStatusAsync(string status);
        
        // Workflow transitions
        Task UpdateStatusAsync(int id, string newStatus, string? actorRole = null, DateTime? timestamp = null);
        Task LogActionAsync(int applicationId, int? actorId, string actorRole, string action, string? notes);

        // Document handling
        Task AddDocumentAsync(ApplicationDocument doc);
    }
}
