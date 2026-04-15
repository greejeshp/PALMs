using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palms.Api.Models.DTOs;
using Palms.Api.Models.Entities;
using Palms.Api.Repositories;
using Palms.Api.Services;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace Palms.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly ApplicationWorkflowService _workflowService;
        private readonly IApplicationRepository _appRepo;

        public ApplicationsController(ApplicationWorkflowService workflowService, IApplicationRepository appRepo)
        {
            _workflowService = workflowService;
            _appRepo = appRepo;
        }

        // --- PUBLIC / TRACKING ROUTES --- 

        [HttpGet("track/{refNumber}")]
        public async Task<IActionResult> TrackStatus(string refNumber)
        {
            var app = await _appRepo.GetApplicationByRefAsync(refNumber);
            if (app == null) return NotFound(new { Error = "Application not found" });

            return Ok(new ApplicationStatusDto
            {
                Id = app.Id,
                ReferenceNumber = app.ReferenceNumber,
                Status = app.Status,
                FirmName = app.FirmName,
                ApplicationType = app.ApplicationType,
                CreatedAt = app.CreatedAt
            });
        }

        // --- STAFF ROUTES ---

        [Authorize(Roles = "ADMIN,AKC_OFFICIAL,PPO,CHIEF")]
        [HttpGet("queue")]
        public async Task<IActionResult> GetQueue([FromQuery] string status)
        {
            if (string.IsNullOrEmpty(status)) return BadRequest("Status query required");

            var role = User.FindFirst("role")?.Value;
            var akcIdClaim = User.FindFirst("akc_id")?.Value;

            IEnumerable<ApplicationStatusDto> queue;

            if (role == "AKC_OFFICIAL" && !string.IsNullOrEmpty(akcIdClaim))
            {
                int akcId = int.Parse(akcIdClaim);
                queue = await _appRepo.GetApplicationsByAkcAndStatusAsync(akcId, status);
            }
            else
            {
                queue = await _appRepo.GetApplicationsByStatusAsync(status);
            }

            return Ok(queue);
        }

        [Authorize(Roles = "AKC_OFFICIAL")]
        [HttpPost("{id}/akc-review")]
        public async Task<IActionResult> AkcReview(int id, [FromBody] WorkflowActionDto req)
        {
            int staffId = int.Parse(User.FindFirst("id")!.Value);
            var (success, error) = await _workflowService.ReviewAtAkcAsync(id, staffId, req.IsApproved, req.Remarks);
            
            if (!success) return BadRequest(new { Error = error });
            return Ok(new { Message = "AKC review recorded." });
        }

        [Authorize(Roles = "PPO")]
        [HttpPost("{id}/ppo-review")]
        public async Task<IActionResult> PpoReview(int id, [FromBody] WorkflowActionDto req)
        {
            int staffId = int.Parse(User.FindFirst("id")!.Value);
            var (success, error) = await _workflowService.ReviewAtPpoAsync(id, staffId, req.IsApproved, req.Remarks);
            
            if (!success) return BadRequest(new { Error = error });
            return Ok(new { Message = "PPO review recorded." });
        }

        [Authorize(Roles = "CHIEF")]
        [HttpPost("{id}/chief-approval")]
        public async Task<IActionResult> ChiefApproval(int id, [FromBody] WorkflowActionDto req)
        {
            int staffId = int.Parse(User.FindFirst("id")!.Value);
            var (success, error) = await _workflowService.ApproveByChiefAsync(id, staffId, req.IsApproved, req.Remarks);
            
            if (!success) return BadRequest(new { Error = error });
            return Ok(new { Message = "Chief approval recorded." });
        }
    }

    public class WorkflowActionDto
    {
        public bool IsApproved { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }
}
