using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palms.Api.Services;
using Palms.Api.Repositories;

namespace Palms.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LicensesController : ControllerBase
    {
        private readonly LicenseGeneratorService _licenseService;
        private readonly ILicenseRepository _licenseRepo;

        public LicensesController(LicenseGeneratorService licenseService, ILicenseRepository licenseRepo)
        {
            _licenseService = licenseService;
            _licenseRepo = licenseRepo;
        }

        [Authorize(Roles = "CHIEF,ADMIN")]
        [HttpPost("issue/{applicationId}")]
        public async Task<IActionResult> IssueLicense(int applicationId)
        {
            try 
            {
                int chiefId = int.Parse(User.FindFirst("id")!.Value);
                string pdfUrl = await _licenseService.IssueLicenseAsync(applicationId, chiefId);
                return Ok(new { Message = "License generated successfully", PdfUrl = pdfUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("{licenseNumber}/download")]
        public async Task<IActionResult> DownloadLicense(string licenseNumber)
        {
            var license = await _licenseRepo.GetByLicenseNumberAsync(licenseNumber);
            if (license == null) return NotFound(new { Error = "License not found" });

            if (string.IsNullOrEmpty(license.PdfPath))
                return BadRequest(new { Error = "PDF file not generated yet" });

            // Resolve physical path
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", license.PdfPath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath)) 
                return NotFound(new { Error = "PDF file physically missing on server" });

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(bytes, "application/pdf", $"{licenseNumber}.pdf");
        }

        [HttpGet("lookup/{licenseNumber}")]
        public async Task<IActionResult> LookupLicense(string licenseNumber)
        {
            var detail = await _licenseRepo.GetLicenseDetailByNumberAsync(licenseNumber);
            if (detail == null) return NotFound(new { Error = "License Number not found or invalid" });
            return Ok(detail);
        }
    }
}
