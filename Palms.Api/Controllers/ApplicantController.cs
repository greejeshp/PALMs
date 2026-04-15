using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palms.Api.Models.DTOs;
using Palms.Api.Repositories;
using Palms.Api.Services;
using Palms.Api.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace Palms.Api.Controllers
{
    [ApiController]
    [Route("api/applicant")]
    [Authorize(Roles = "APPLICANT")]
    public class ApplicantController : ControllerBase
    {
        private readonly IApplicationRepository _appRepo;
        private readonly IApplicantRepository _applicantRepo;
        private readonly ApplicationWorkflowService _workflowService;
        private readonly ILicenseRepository _licenseRepo;
        private readonly OtpService _otpService;
        private readonly IWebHostEnvironment _env;

        public ApplicantController(
            IApplicationRepository appRepo, 
            IApplicantRepository applicantRepo,
            ApplicationWorkflowService workflowService,
            ILicenseRepository licenseRepo,
            OtpService otpService,
            IWebHostEnvironment env)
        {
            _appRepo = appRepo;
            _applicantRepo = applicantRepo;
            _workflowService = workflowService;
            _licenseRepo = licenseRepo;
            _otpService = otpService;
            _env = env;
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetMyApplications()
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(idClaim)) return Unauthorized();
            
            int applicantId = int.Parse(idClaim);
            
            var apps = await _appRepo.GetApplicationsForApplicantAsync(applicantId);
            
            // Map to camelCase/snake_case for frontend if needed, 
            // but Dapper result is usually fine if properties match DTO.
            return Ok(apps.Select(a => new {
                id = a.Id,
                reference_number = a.ReferenceNumber,
                status = a.Status,
                firm_name = a.FirmName,
                application_type = a.ApplicationType,
                license_category = a.LicenseCategory,
                created_at = a.CreatedAt,
                license_number = a.LicenseNumber,
                pdf_path = a.PdfPath
            }));
        }

        [HttpGet("licenses")]
        public async Task<IActionResult> GetMyLicenses()
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(idClaim)) return Unauthorized();
            
            int applicantId = int.Parse(idClaim);
            var licenses = await _licenseRepo.GetLicensesByApplicantIdAsync(applicantId);
            
            return Ok(licenses.Select(l => new {
                id = l.Id,
                license_number = l.LicenseNumber,
                firm_name = l.FirmName,
                issue_date = l.IssueDate,
                expiry_date = l.ExpiryDate,
                status = l.Status,
                pdf_path = l.PdfPath
            }));
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(idClaim)) return Unauthorized();

            int applicantId = int.Parse(idClaim);
            var profile = await _applicantRepo.GetApplicantProfileAsync(applicantId);
            
            if (profile == null) return NotFound();

            return Ok(new {
                id = profile.Id,
                mobile = profile.Mobile,
                full_name = profile.FullName,
                is_verified = profile.IsVerified,
                firm_name = profile.FirmName,
                address_district = profile.AddressDistrict,
                profile_complete = profile.ProfileComplete
            });
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitApplication([FromForm] ApplicationSubmitDto req)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(idClaim)) return Unauthorized();

            int applicantId = int.Parse(idClaim);

            var app = new Application
            {
                ApplicantId = applicantId,
                ApplicationType = req.ApplicationType,
                LicenseCategory = req.LicenseCategory,
                FirmName = req.FirmName,
                RegistrationNumber = req.RegistrationNumber,
                PanVatNumber = req.PanVatNumber,
                AuthorizedPerson = req.AuthorizedPerson,
                CitizenshipNumber = req.CitizenshipNumber,
                AddressDistrict = req.AddressDistrict,
                AddressGapaNapa = req.AddressGapaNapa,
                Phone = req.Phone,
                BusinessDescription = req.BusinessDescription,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            int appId = await _workflowService.SubmitNewApplicationAsync(app);

            // Handle file uploads
            if (Request.Form.Files.Count > 0)
            {
                string uploadPath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                foreach (var file in Request.Form.Files)
                {
                    if (file.Length > 0)
                    {
                        string originalFilename = file.FileName;
                        string extension = Path.GetExtension(originalFilename);
                        string storedFilename = $"{Guid.NewGuid()}{extension}";
                        string filePath = Path.Combine(uploadPath, storedFilename);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Save document record
                        var doc = new ApplicationDocument
                        {
                            ApplicationId = appId,
                            DocType = MapDocType(file.Name),
                            OriginalFilename = originalFilename,
                            StoredFilename = storedFilename,
                            FilePath = $"/uploads/{storedFilename}",
                            FileSize = file.Length,
                            MimeType = file.ContentType,
                            UploadedAt = DateTime.UtcNow
                        };

                        await _appRepo.AddDocumentAsync(doc);
                    }
                }
            }

            return Ok(new { Message = "Application submitted successfully", ApplicationId = appId });
        }

        [HttpPost("request-profile-update")]
        public async Task<IActionResult> RequestProfileUpdate([FromBody] ProfileUpdateRequest req)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(idClaim)) return Unauthorized();
            int applicantId = int.Parse(idClaim);

            if (string.IsNullOrEmpty(req.NewValue)) return BadRequest(new { Error = "New value required." });

            var applicant = await _applicantRepo.GetByIdAsync(applicantId);
            if (applicant == null) return NotFound();

            // Store OTP. We use the NEW value as the target for OTP (simulated).
            // This is safer as we verify they OWN the new mobile/email.
            var otpTarget = req.NewValue; 
            var devOtp = await _otpService.GenerateAndSendOtpAsync(applicantId, otpTarget, "PROFILE_UPDATE");

            return Ok(new { Message = "Verification OTP sent.", DevOtp = devOtp });
        }

        [HttpPost("verify-profile-update")]
        public async Task<IActionResult> VerifyProfileUpdate([FromBody] ProfileUpdateVerifyRequest req)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(idClaim)) return Unauthorized();
            int applicantId = int.Parse(idClaim);

            var (isValid, error) = await _otpService.VerifyOtpAsync(req.NewValue, req.OtpCode, "PROFILE_UPDATE");
            if (!isValid) return BadRequest(new { Error = error });

            if (req.Type == "MOBILE")
            {
                await _applicantRepo.UpdateMobileAsync(applicantId, req.NewValue);
            }
            else
            {
                await _applicantRepo.UpdateEmailAsync(applicantId, req.NewValue);
            }

            return Ok(new { Message = "Profile updated successfully." });
        }

        private string MapDocType(string frontendKey)
        {
            return frontendKey.ToLower() switch
            {
                "company_reg_govt" => "FIRM_REGISTRATION",
                "ana_company_reg" => "FIRM_REGISTRATION",
                "pan_vat_govt" => "PAN_VAT_CERTIFICATE",
                "ana_vat_tax" => "PAN_VAT_CERTIFICATE",
                "training_cert" => "TRAINING_CERTIFICATE",
                "prior_license" => "EXISTING_LICENSE",
                "business_description" => "BUSINESS_DESCRIPTION",
                "fee_receipt" => "PAYMENT_RECEIPT",
                _ => "OTHER"
            };
        }
    }
}
