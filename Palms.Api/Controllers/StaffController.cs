using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palms.Api.Data;
using Palms.Api.Models.Entities;
using System.Text.Json.Serialization;
using Palms.Api.Services;

namespace Palms.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IDbConnectionFactory _connectionFactory;
        public ReportsController(IDbConnectionFactory cf) => _connectionFactory = cf;

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var idClaim = User.FindFirst("id")?.Value;
            var role = User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(idClaim)) return Unauthorized();
            int userId = int.Parse(idClaim);

            using var conn = _connectionFactory.CreateConnection();
            
            // Get AKC assignments if user is AKC_OFFICIAL
            var akcIds = new List<int>();
            if (role == "AKC_OFFICIAL")
            {
                akcIds = (await conn.QueryAsync<int>("SELECT AkcId FROM UserAkcs WHERE UserId = @UserId", new { UserId = userId })).ToList();
            }

            // Build conditional filters
            string appFilter = "";
            var p = new DynamicParameters();
            if (role == "AKC_OFFICIAL")
            {
                if (akcIds.Count > 0)
                {
                    appFilter = " WHERE AssignedAkcId IN @AkcIds ";
                    p.Add("AkcIds", akcIds);
                }
                else
                {
                    // Fallback: If AKC Official has no assignments, show nothing
                    appFilter = " WHERE 1=0 ";
                }
            }

            var stats = await conn.QuerySingleAsync(@"
                SELECT COUNT(*) AS Total,
                    COALESCE(SUM(CASE WHEN Status='SUBMITTED' THEN 1 ELSE 0 END), 0) AS Submitted,
                    COALESCE(SUM(CASE WHEN Status='AKC_REVIEW' THEN 1 ELSE 0 END), 0) AS AkcReview,
                    COALESCE(SUM(CASE WHEN Status='REVENUE_CHECK' THEN 1 ELSE 0 END), 0) AS RevenueCheck,
                    COALESCE(SUM(CASE WHEN Status='PPO_REVIEW' THEN 1 ELSE 0 END), 0) AS PpoReview,
                    COALESCE(SUM(CASE WHEN Status='CHIEF_APPROVAL' THEN 1 ELSE 0 END), 0) AS ChiefApproval,
                    COALESCE(SUM(CASE WHEN Status='ISSUED' THEN 1 ELSE 0 END), 0) AS Issued,
                    COALESCE(SUM(CASE WHEN Status='REJECTED' THEN 1 ELSE 0 END), 0) AS Rejected
                FROM Applications" + appFilter, p);

            var licenseStats = await conn.QuerySingleAsync(@"
                SELECT
                    COALESCE(SUM(CASE WHEN l.Status='ACTIVE' THEN 1 ELSE 0 END), 0) AS Active,
                    COALESCE(SUM(CASE WHEN l.Status='SUSPENDED' THEN 1 ELSE 0 END), 0) AS Suspended,
                    COALESCE(SUM(CASE WHEN l.ExpiryDate BETWEEN GETUTCDATE() AND DATEADD(DAY, 30, GETUTCDATE()) THEN 1 ELSE 0 END), 0) AS Expiring30Days
                FROM Licenses l" + (role == "AKC_OFFICIAL" ? " JOIN Applications a ON l.ApplicationId = a.Id " + appFilter.Replace("WHERE", "AND") : ""), p);

            var rev = await conn.QuerySingleAsync(
                "SELECT COALESCE(SUM(PaymentAmount), 0) AS TotalCollected FROM Applications " + appFilter + (string.IsNullOrEmpty(appFilter) ? " WHERE " : " AND ") + "PaymentConfirmed = 1", p);

            var byDistrict = await conn.QueryAsync(@"
                SELECT AddressDistrict AS district, COUNT(*) AS count
                FROM Applications " + appFilter + @"
                GROUP BY AddressDistrict ORDER BY count DESC", p);

            var monthlyTrend = await conn.QueryAsync(@"
                SELECT FORMAT(SubmittedAt, 'MMM') AS month, COUNT(*) AS count
                FROM Applications
                " + appFilter + (string.IsNullOrEmpty(appFilter) ? " WHERE " : " AND ") + @" SubmittedAt >= DATEADD(MONTH, -5, GETUTCDATE())
                GROUP BY FORMAT(SubmittedAt, 'MMM'), MONTH(SubmittedAt)
                ORDER BY MIN(SubmittedAt)", p);

            var recentActivity = await conn.QueryAsync(@"
                SELECT TOP 10 wa.Action, wa.CreatedAt,
                       COALESCE(u.FullName, 'Applicant') AS actor,
                       COALESCE(u.Role, '') AS actor_role,
                       a.FirmName AS firm_name, a.ReferenceNumber AS reference_number
                FROM WorkflowActions wa
                JOIN Applications a ON wa.ApplicationId = a.Id
                LEFT JOIN Users u ON wa.ActorId = u.Id" +
                (role == "AKC_OFFICIAL" ? " WHERE a.AssignedAkcId IN @AkcIds " : "") + @"
                ORDER BY wa.CreatedAt DESC", p);

            return Ok(new {
                stats = new {
                    total = stats.Total, submitted = stats.Submitted, akc_review = stats.AkcReview,
                    revenue_check = stats.RevenueCheck, ppo_review = stats.PpoReview,
                    chief_approval = stats.ChiefApproval, issued = stats.Issued, rejected = stats.Rejected
                },
                licenseStats = new {
                    active = licenseStats.Active, suspended = licenseStats.Suspended,
                    expiring_30_days = licenseStats.Expiring30Days
                },
                revenue = new { total_collected = rev.TotalCollected },
                byDistrict, monthlyTrend,
                recentActivity = recentActivity.Select(r => new {
                    action = r.Action, created_at = r.CreatedAt,
                    actor = r.actor, actor_role = r.actor_role,
                    firm_name = r.firm_name, reference_number = r.reference_number
                })
            });
        }

        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast()
        {
            using var conn = _connectionFactory.CreateConnection();
            var next30 = await conn.QueryAsync(@"
                SELECT LicenseNumber AS license_number, FirmName AS firm_name,
                       AddressDistrict AS address_district,
                       CONVERT(VARCHAR(10), ExpiryDate, 120) AS expiry_date
                FROM Licenses WHERE Status='ACTIVE'
                AND ExpiryDate BETWEEN GETDATE() AND DATEADD(DAY,30,GETDATE())");

            var next60 = await conn.QueryAsync(@"
                SELECT LicenseNumber AS license_number, FirmName AS firm_name,
                       AddressDistrict AS address_district,
                       CONVERT(VARCHAR(10), ExpiryDate, 120) AS expiry_date
                FROM Licenses WHERE Status='ACTIVE'
                AND ExpiryDate BETWEEN DATEADD(DAY,31,GETDATE()) AND DATEADD(DAY,60,GETDATE())");

            var next90 = await conn.QueryAsync(@"
                SELECT LicenseNumber AS license_number, FirmName AS firm_name,
                       AddressDistrict AS address_district,
                       CONVERT(VARCHAR(10), ExpiryDate, 120) AS expiry_date
                FROM Licenses WHERE Status='ACTIVE'
                AND ExpiryDate BETWEEN DATEADD(DAY,61,GETDATE()) AND DATEADD(DAY,90,GETDATE())");

            return Ok(new { next_30 = next30, next_60 = next60, next_90 = next90 });
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetReportApps(
            [FromQuery] string? from_date, [FromQuery] string? to_date, [FromQuery] string? district)
        {
            using var conn = _connectionFactory.CreateConnection();
            var conditions = new List<string> { "1=1" };
            var p = new DynamicParameters();
            if (!string.IsNullOrEmpty(from_date)) { conditions.Add("SubmittedAt >= @From"); p.Add("From", from_date); }
            if (!string.IsNullOrEmpty(to_date))   { conditions.Add("SubmittedAt <= @To"); p.Add("To", to_date); }
            if (!string.IsNullOrEmpty(district))  { conditions.Add("AddressDistrict = @District"); p.Add("District", district); }

            var apps = await conn.QueryAsync(
                $"SELECT ReferenceNumber AS reference_number, FirmName AS firm_name, ApplicationType AS application_type, AddressDistrict AS address_district, Status AS status, PaymentAmount AS payment_amount, IsLateSubmission AS is_late_submission, SubmittedAt AS submitted_at FROM Applications WHERE {string.Join(" AND ", conditions)} ORDER BY SubmittedAt DESC", p);

            return Ok(new { applications = apps });
        }

        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportCsv()
        {
            using var conn = _connectionFactory.CreateConnection();
            var apps = await conn.QueryAsync("SELECT ReferenceNumber, FirmName, ApplicationType, AddressDistrict, Status, PaymentAmount, SubmittedAt FROM Applications ORDER BY SubmittedAt DESC");
            var csv = "Reference,Firm,Type,District,Status,Fee,Submitted\n" +
                string.Join("\n", apps.Select(a => $"{a.ReferenceNumber},{a.FirmName},{a.ApplicationType},{a.AddressDistrict},{a.Status},{a.PaymentAmount},{a.SubmittedAt}"));
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "palms_report.csv");
        }
    }

    // ============================================================
    // AUDIT LOGS: GET /api/audit
    // ============================================================
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "ADMIN")]
    public class AuditController : ControllerBase
    {
        private readonly IDbConnectionFactory _connectionFactory;
        public AuditController(IDbConnectionFactory cf) => _connectionFactory = cf;

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] string? actor_type, [FromQuery] string? action,
            [FromQuery] string? from_date, [FromQuery] string? to_date, [FromQuery] int page = 1)
        {
            using var conn = _connectionFactory.CreateConnection();
            var conditions = new List<string> { "1=1" };
            var p = new DynamicParameters();
            if (!string.IsNullOrEmpty(actor_type)) { conditions.Add("ActorType=@ActorType"); p.Add("ActorType", actor_type); }
            if (!string.IsNullOrEmpty(action))     { conditions.Add("Action LIKE @Action"); p.Add("Action", $"%{action}%"); }
            if (!string.IsNullOrEmpty(from_date))  { conditions.Add("CreatedAt>=@From"); p.Add("From", from_date); }
            if (!string.IsNullOrEmpty(to_date))    { conditions.Add("CreatedAt<=@To"); p.Add("To", to_date); }

            var where = "WHERE " + string.Join(" AND ", conditions);
            p.Add("Offset", (page - 1) * 50);

            var sql = $@"
                SELECT Id, ActorType, ActorId, ActorRole, Action, EntityType, EntityId, IpAddress, CreatedAt
                FROM AuditLogs {where} ORDER BY CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT 50 ROWS ONLY;
                SELECT COUNT(*) FROM AuditLogs {where};";

            using var multi = await conn.QueryMultipleAsync(sql, p);
            var logs = await multi.ReadAsync();
            var total = await multi.ReadFirstAsync<int>();

            return Ok(new {
                logs = logs.Select(l => new {
                    id = l.Id, actor_type = l.ActorType, actor_id = l.ActorId,
                    actor_role = l.ActorRole, action = l.Action,
                    entity_type = l.EntityType, entity_id = l.EntityId,
                    ip_address = l.IpAddress, created_at = l.CreatedAt
                }),
                total
            });
        }
    }

    // ============================================================
    // USERS: GET/POST/PUT /api/users
    // ============================================================
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "ADMIN")]
    public class UsersController : ControllerBase
    {
        private readonly IDbConnectionFactory _connectionFactory;
        public UsersController(IDbConnectionFactory cf) => _connectionFactory = cf;

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            using var conn = _connectionFactory.CreateConnection();
            var users = await conn.QueryAsync(@"
                SELECT Id AS id,
                       Username AS username,
                       Email AS email,
                       FullName AS full_name,
                       Role AS role,
                       District AS district,
                       AkcId AS akc_id,
                       IsActive AS is_active,
                       IsLocked AS is_locked,
                       LastLoginAt AS last_login_at
                FROM Users ORDER BY Id");
            var akcs = await conn.QueryAsync("SELECT Id AS id, Name AS name, District AS district FROM Akcs WHERE IsActive=1");
            // Also fetch multi-AKC assignments
            var userAkcs = await conn.QueryAsync("SELECT UserId AS user_id, AkcId AS akc_id FROM UserAkcs");
            var akcMap = userAkcs.GroupBy(x => (int)x.user_id)
                                  .ToDictionary(g => g.Key, g => g.Select(x => (int)x.akc_id).ToList());
            var enriched = users.Select(u => new {
                id          = (int)u.id,
                username    = (string)u.username,
                email       = (string)u.email,
                full_name   = (string)u.full_name,
                role        = (string)u.role,
                district    = (string?)u.district,
                akc_id      = (int?)u.akc_id,
                is_active   = (bool)u.is_active,
                is_locked   = (bool)u.is_locked,
                last_login_at = u.last_login_at == null ? "-" : ((DateTime)u.last_login_at).ToString("o"),
                assigned_akc_ids = akcMap.TryGetValue((int)u.id, out var ids) ? ids : new List<int>()
            });
            return Ok(new { users = enriched, akcs });
        }


        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            using var conn = _connectionFactory.CreateConnection();
            var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 11);
            // Use first AkcId as primary for legacy column
            var primaryAkc = dto.AkcIds?.FirstOrDefault() ?? dto.AkcId;
            var userId = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Users (Username, Email, PasswordHash, FullName, Role, District, AkcId, IsActive)
                OUTPUT INSERTED.Id
                VALUES (@Username, @Email, @Hash, @FullName, @Role, @District, @AkcId, 1)",
                new { dto.Username, dto.Email, Hash = hash, dto.FullName, dto.Role, dto.District, AkcId = (object?)primaryAkc ?? DBNull.Value });
            // Insert multiple AKC assignments
            var akcList = dto.AkcIds?.Any() == true ? dto.AkcIds : (primaryAkc.HasValue ? new List<int> { primaryAkc.Value } : new List<int>());
            foreach (var akcId in akcList)
                await conn.ExecuteAsync("INSERT INTO UserAkcs (UserId, AkcId) VALUES (@UserId, @AkcId)", new { UserId = userId, AkcId = akcId });
            return Ok(new { Message = "User created" });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            using var conn = _connectionFactory.CreateConnection();
            
            var updates = new List<string>();
            var p = new DynamicParameters();
            p.Add("Id", id);

            if (dto.Username != null) { updates.Add("Username = @Username"); p.Add("Username", dto.Username); }
            if (dto.Email != null)    { updates.Add("Email = @Email"); p.Add("Email", dto.Email); }
            if (dto.FullName != null) { updates.Add("FullName = @FullName"); p.Add("FullName", dto.FullName); }
            if (dto.Role != null)     { updates.Add("Role = @Role"); p.Add("Role", dto.Role); }
            if (dto.District != null) { updates.Add("District = @District"); p.Add("District", dto.District); }
            if (dto.IsActive.HasValue){ updates.Add("IsActive = @Active"); p.Add("Active", dto.IsActive.Value); }
            if (dto.IsLocked.HasValue){ updates.Add("IsLocked = @Locked"); p.Add("Locked", dto.IsLocked.Value == 1); if(dto.IsLocked.Value == 0) updates.Add("FailedLoginCount = 0"); }

            if (updates.Count > 0)
            {
                string sql = $"UPDATE Users SET {string.Join(", ", updates)}, UpdatedAt = GETUTCDATE() WHERE Id = @Id";
                await conn.ExecuteAsync(sql, p);
            }

            // Handle multi-AKC assignment sync
            if (dto.AkcIds != null)
            {
                // Update legacy single AkcId to first assigned
                var primaryAkc = dto.AkcIds.FirstOrDefault();
                await conn.ExecuteAsync("UPDATE Users SET AkcId = @AkcId, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                    new { AkcId = dto.AkcIds.Any() ? (object)primaryAkc : DBNull.Value, Id = id });

                // Sync UserAkcs junction table
                await conn.ExecuteAsync("DELETE FROM UserAkcs WHERE UserId = @Id", new { Id = id });
                foreach (var akcId in dto.AkcIds)
                    await conn.ExecuteAsync("INSERT INTO UserAkcs (UserId, AkcId) VALUES (@UserId, @AkcId)", new { UserId = id, AkcId = akcId });
            }
            
            return Ok(new { Message = "User updated successfully" });
        }

        [HttpPost("{id:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
        {
            using var conn = _connectionFactory.CreateConnection();
            var hash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 11);
            await conn.ExecuteAsync("UPDATE Users SET PasswordHash=@Hash WHERE Id=@Id", new { Hash = hash, Id = id });
            return Ok(new { Message = "Password reset" });
        }
    }

    public class CreateUserDto
    {
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public string? District { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("akc_id")]
        public int? AkcId { get; set; } // legacy single
        [System.Text.Json.Serialization.JsonPropertyName("akc_ids")]
        public List<int>? AkcIds { get; set; } // multi
        public string Password { get; set; } = "";
    }

    public class UpdateUserDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? District { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("akc_ids")]
        public List<int>? AkcIds { get; set; } // replaces single AkcId
        public int? IsActive { get; set; }
        public int? IsLocked { get; set; }
    }

    public class ResetPasswordDto
    {
        public string NewPassword { get; set; } = "";
    }
}

// ============================================================
// ADMIN: Applicant Password Reset
// POST /api/admin/applicants/{id}/reset-password
// ============================================================
namespace Palms.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "ADMIN")]
    public class AdminApplicantsController : ControllerBase
    {
        private readonly IDbConnectionFactory _connectionFactory;
        public AdminApplicantsController(IDbConnectionFactory cf) => _connectionFactory = cf;

        [HttpPost("applicants/{applicantId:int}/reset-password")]
        public async Task<IActionResult> ResetApplicantPassword(int applicantId, [FromBody] ResetApplicantPasswordDto dto)
        {
            using var conn = _connectionFactory.CreateConnection();
            var applicant = await conn.QuerySingleOrDefaultAsync(
                "SELECT Id, FullName, Mobile FROM Applicants WHERE Id = @Id", new { Id = applicantId });
            if (applicant == null) return NotFound(new { Error = "Applicant not found" });

            var hash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 11);
            await conn.ExecuteAsync(
                "UPDATE Applicants SET PasswordHash = @Hash, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Hash = hash, Id = applicantId });

            var adminId = User.FindFirst("id")?.Value;
            await conn.ExecuteAsync(@"
                INSERT INTO AuditLogs (ActorType, ActorId, ActorRole, Action, EntityType, EntityId, CreatedAt)
                VALUES ('STAFF', @ActorId, 'ADMIN', 'APPLICANT_PWD_RESET', 'Applicant', @AppId, GETUTCDATE())",
                new { ActorId = adminId != null ? int.Parse(adminId) : (int?)null, AppId = applicantId });

            return Ok(new { Message = $"Password reset for applicant {applicant.FullName}" });
        }

        [HttpPatch("applicants/{applicantId:int}/status")]
        public async Task<IActionResult> UpdateApplicantStatus(int applicantId, [FromBody] UpdateStatusDto dto)
        {
            using var conn = _connectionFactory.CreateConnection();
            var rows = await conn.ExecuteAsync(
                "UPDATE Applicants SET IsActive = @IsActive, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { IsActive = dto.IsActive ? 1 : 0, Id = applicantId });
            
            if (rows == 0) return NotFound(new { Error = "Applicant not found" });

            var adminId = User.FindFirst("id")?.Value;
            await conn.ExecuteAsync(@"
                INSERT INTO AuditLogs (ActorType, ActorId, ActorRole, Action, EntityType, EntityId, CreatedAt)
                VALUES ('STAFF', @ActorId, 'ADMIN', 'APPLICANT_STATUS_UPDATE', 'Applicant', @AppId, GETUTCDATE())",
                new { ActorId = adminId != null ? int.Parse(adminId) : (int?)null, AppId = applicantId });

            return Ok(new { Message = $"Applicant status updated to {(dto.IsActive ? "Active" : "Inactive")}" });
        }

        [HttpPatch("applicants/{applicantId:int}/verify")]
        public async Task<IActionResult> UpdateApplicantVerification(int applicantId, [FromBody] UpdateVerificationDto dto)
        {
            using var conn = _connectionFactory.CreateConnection();
            var rows = await conn.ExecuteAsync(
                "UPDATE Applicants SET IsVerified = @IsVerified, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { IsVerified = dto.IsVerified ? 1 : 0, Id = applicantId });

            if (rows == 0) return NotFound(new { Error = "Applicant not found" });

            var adminId = User.FindFirst("id")?.Value;
            await conn.ExecuteAsync(@"
                INSERT INTO AuditLogs (ActorType, ActorId, ActorRole, Action, EntityType, EntityId, CreatedAt)
                VALUES ('STAFF', @ActorId, 'ADMIN', 'APPLICANT_VERIFY_UPDATE', 'Applicant', @AppId, GETUTCDATE())",
                new { ActorId = adminId != null ? int.Parse(adminId) : (int?)null, AppId = applicantId });

            return Ok(new { Message = $"Applicant verification updated to {(dto.IsVerified ? "Verified" : "Unverified")}" });
        }
    }

    public class UpdateStatusDto 
    { 
        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; } 
    }
    public class UpdateVerificationDto 
    { 
        [JsonPropertyName("is_verified")]
        public bool IsVerified { get; set; } 
    }

    public class ResetApplicantPasswordDto
    {
        [JsonPropertyName("new_password")]
        public string NewPassword { get; set; } = "";
    }
}
