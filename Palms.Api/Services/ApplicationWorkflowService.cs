using Palms.Api.Models.Entities;
using Palms.Api.Repositories;

namespace Palms.Api.Services
{
    public class ApplicationWorkflowService
    {
        private readonly IApplicationRepository _appRepo;

        public ApplicationWorkflowService(IApplicationRepository appRepo)
        {
            _appRepo = appRepo;
        }

        public async Task<int> SubmitNewApplicationAsync(Application app)
        {
            app.Status = "SUBMITTED";
            app.ApplicationType = "NEW";
            return await _appRepo.CreateApplicationAsync(app);
        }

        public async Task<(bool success, string? error)> ReviewAtAkcAsync(int appId, int staffId, bool isApproved, string remarks)
        {
            var app = await _appRepo.GetApplicationByIdAsync(appId);
            if (app == null) return (false, "Not found");
            if (app.Status != "SUBMITTED") return (false, "Application is not in SUBMITTED state");

            string newStatus = isApproved ? "AKC_REVIEW" : "RETURNED";
            string action = isApproved ? "AKC_APPROVED" : "AKC_RETURNED";

            await _appRepo.UpdateStatusAsync(appId, newStatus, "AKC_OFFICIAL", DateTime.UtcNow);
            await _appRepo.LogActionAsync(appId, staffId, "AKC_OFFICIAL", action, remarks);

            return (true, null);
        }

        public async Task<(bool success, string? error)> ReviewAtPpoAsync(int appId, int staffId, bool isApproved, string remarks)
        {
            var app = await _appRepo.GetApplicationByIdAsync(appId);
            if (app == null) return (false, "Not found");
            
            // PPO reviews after AKC has approved and Revenue checked (in a full workflow, Revenue is separate)
            // For MVP, we'll allow PPO review directly from AKC_REVIEW
            if (app.Status != "AKC_REVIEW") return (false, "Application is not in AKC_REVIEW state");

            string newStatus = isApproved ? "PPO_REVIEW" : "RETURNED";
            string action = isApproved ? "PPO_APPROVED" : "PPO_RETURNED";

            await _appRepo.UpdateStatusAsync(appId, newStatus, "PPO", DateTime.UtcNow);
            await _appRepo.LogActionAsync(appId, staffId, "PPO", action, remarks);

            return (true, null);
        }

        public async Task<(bool success, string? error)> ApproveByChiefAsync(int appId, int staffId, bool isApproved, string remarks)
        {
            var app = await _appRepo.GetApplicationByIdAsync(appId);
            if (app == null) return (false, "Not found");
            if (app.Status != "PPO_REVIEW") return (false, "Application must be PPO approved first");

            string newStatus = isApproved ? "CHIEF_APPROVAL" : "REJECTED";
            string action = isApproved ? "CHIEF_APPROVED" : "CHIEF_REJECTED";

            await _appRepo.UpdateStatusAsync(appId, newStatus, "CHIEF", DateTime.UtcNow);
            await _appRepo.LogActionAsync(appId, staffId, "CHIEF", action, remarks);

            return (true, null);
        }
    }
}
