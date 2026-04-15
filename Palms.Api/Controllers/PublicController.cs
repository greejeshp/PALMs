using Microsoft.AspNetCore.Mvc;
using Palms.Api.Repositories;

namespace Palms.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PublicController : ControllerBase
    {
        private readonly ILicenseRepository _licenseRepo;

        public PublicController(ILicenseRepository licenseRepo)
        {
            _licenseRepo = licenseRepo;
        }

        [HttpGet("verify")]
        public async Task<IActionResult> VerifyLicense([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { Error = "Search query is required." });

            var license = await _licenseRepo.GetByLicenseNumberAsync(query);
            
            if (license == null)
            {
                // Simple search fall-over: since we don't have Full-Text configured in this MVP schema,
                // we'll just check if query matches the license number perfectly. If not, return empty.
                return Ok(new object[] { });
            }

            var result = new
            {
                license_number = license.LicenseNumber,
                firm_name = license.FirmName,
                seller_name = license.SellerName,
                address_district = license.AddressDistrict,
                address = license.Address,
                issue_date = license.IssueDate.ToString("yyyy-MM-dd"),
                expiry_date = license.ExpiryDate.ToString("yyyy-MM-dd"),
                status = license.Status,
                daysToExpiry = (license.ExpiryDate - DateTime.UtcNow.Date).Days,
                suspension_reason = license.SuspensionReason,
                cancellation_reason = license.CancellationReason
            };

            return Ok(new[] { result });
        }
    }
}
