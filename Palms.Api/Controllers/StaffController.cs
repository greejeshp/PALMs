using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palms.Api.Data;
using Palms.Api.Models.Entities;
using System.Text.Json.Serialization;
using Palms.Api.Services;

namespace Palms.Api.Controllers
{
    // ============================================================
    // Applications List & Detail: GET /api/applications
    //                             GET /api/applications/{id}
    //                             POST /api/applications/{id}/action
    // These supplement the existing ApplicationsController routes.
    // ============================================================
    [ApiController]
    [Authorize]
    public class ApplicationsExtController : ControllerBase
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly LicenseGeneratorService _licenseGenerator;
        public ApplicationsExtController(IDbConnectionFactory cf, LicenseGeneratorService lgs) 
        {
            _connectionFactory = cf;
            _licenseGenerator = lgs;
        }

        [HttpGet("api/applications")]
        public async Task<IActionResult> GetApplications(
            [FromQuery] string? status,
            [FromQuery] string? district,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            using var conn = _connectionFactory.CreateConnection();
            var conditions = new List<string>();
            var p = new DynamicParameters();

            var role = User.FindFirst("role")?.Value;
            var akcIdClaim = User.FindFirst("akc_id")?.Value;

            // AKC officials see applications from their assigned districts (UserAkcs)
            if (role == "AKC_OFFICIAL" && !string.IsNullOrEmpty(akcIdClaim))
            {
                // Fetch all AKC IDs assigned to this user
                var userAkcIds = await conn.QueryAsync<int>(
                    "SELECT AkcId FROM UserAkcs WHERE UserId = @UserId",
                    new { UserId = int.Parse(User.FindFirst("id")?.Value ?? "0") });
                var akcList = userAkcIds.ToList();
                if (!akcList.Any()) akcList.Add(int.Parse(akcIdClaim)); // fallback
                var inClause = string.Join(",", akcList.Select((_, i) => $"@AkcId{i}"));
                conditions.Add($"AssignedAkcId IN ({inClause})");
                for (int i = 0; i < akcList.Count; i++) p.Add($"AkcId{i}", akcList[i]);
            }

            if (!string.IsNullOrEmpty(status))   { conditions.Add("Status = @Status"); p.Add("Status", status); }
            if (!string.IsNullOrEmpty(district))  { conditions.Add("AddressDistrict = @District"); p.Add("District", district); }

            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            int offset = (page - 1) * pageSize;
            p.Add("PageSize", pageSize); p.Add("Offset", offset);

            var sql = $@"
                SELECT a.Id, a.ReferenceNumber, a.ApplicationType, a.Status,
                       a.FirmName, a.AuthorizedPerson, a.AddressDistrict,
                       a.PaymentAmount, a.PaymentConfirmed, a.IsLateSubmission, a.SubmittedAt,
                       ap.FullName AS ApplicantName, ap.Mobile AS ApplicantMobile,
                       l.LicenseNumber, l.PdfPath
                FROM Applications a
                LEFT JOIN Applicants ap ON a.ApplicantId = ap.Id
                LEFT JOIN Licenses l ON a.Id = l.ApplicationId
                {where}
                ORDER BY a.SubmittedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
                SELECT COUNT(*) FROM Applications a {where};";

            using var multi = await conn.QueryMultipleAsync(sql, p);
            var apps = await multi.ReadAsync();
            var total = await multi.ReadFirstAsync<int>();

            return Ok(new {
                applications = apps.Select(a => new {
                    id = a.Id, reference_number = a.ReferenceNumber,
                    application_type = a.ApplicationType, status = a.Status,
                    firm_name = a.FirmName, authorized_person = a.AuthorizedPerson,
                    address_district = a.AddressDistrict, payment_amount = a.PaymentAmount,
                    payment_confirmed = a.PaymentConfirmed, is_late_submission = a.IsLateSubmission,
                    submitted_at = a.SubmittedAt, applicant_name = a.ApplicantName,
                    applicant_mobile = a.ApplicantMobile,
                    license_number = a.LicenseNumber,
                    pdf_path = a.PdfPath
                }),
                total
            });
        }

        [HttpGet("api/applications/{id:int}")]
        public async Task<IActionResult> GetApplicationDetail(int id)
        {
            using var conn = _connectionFactory.CreateConnection();

            var app = await conn.QuerySingleOrDefaultAsync(
                @"SELECT a.*, ap.FullName AS ApplicantName, ap.Mobile AS ApplicantMobile, 
                         ap.Email AS ApplicantAccountEmail, ap.IsActive AS ApplicantIsActive, 
                         ap.IsVerified AS ApplicantIsVerified
                  FROM Applications a
                  LEFT JOIN Applicants ap ON a.ApplicantId = ap.Id
                  WHERE a.Id = @Id", new { Id = id });

            if (app == null) return NotFound(new { Error = "Application not found" });

            // Security check for applicants
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
            var userIdClaim = User.FindFirst("id")?.Value;
            if (role == "APPLICANT" && userIdClaim != null)
            {
                int applicantId = int.Parse(userIdClaim);
                if (app.ApplicantId != applicantId)
                {
                    return Forbid();
                }
            }

            var docs = await conn.QueryAsync(
                "SELECT * FROM ApplicationDocuments WHERE ApplicationId = @Id ORDER BY UploadedAt DESC",
                new { Id = id });

            var workflow = await conn.QueryAsync(@"
                SELECT wa.Action, wa.Reason, wa.Notes, wa.CreatedAt,
                       COALESCE(u.FullName, 'Applicant') AS ActorName, wa.ActorRole
                FROM WorkflowActions wa
                LEFT JOIN Users u ON wa.ActorId = u.Id
                WHERE wa.ApplicationId = @Id ORDER BY wa.CreatedAt ASC", new { Id = id });

            var license = await conn.QuerySingleOrDefaultAsync(
                "SELECT * FROM Licenses WHERE ApplicationId = @Id", new { Id = id });

            var checklist = await conn.QuerySingleOrDefaultAsync(
                "SELECT * FROM ChecklistResponses WHERE ApplicationId = @Id", new { Id = id });

            return Ok(new {
                application = new {
                    id = app.Id,
                    reference_number = app.ReferenceNumber,
                    applicant_id = app.ApplicantId,
                    application_type = app.ApplicationType,
                    status = app.Status,
                    firm_name = app.FirmName,
                    registration_number = app.RegistrationNumber,
                    pan_vat_number = app.PanVatNumber,
                    prior_license_number = app.PriorLicenseNumber,
                    prior_license_expiry = app.PriorLicenseExpiry,
                    authorized_person = app.AuthorizedPerson,
                    citizenship_number = app.CitizenshipNumber,
                    designation = app.Designation,
                    address_gapa_napa = app.AddressGapaNapa,
                    address_ward = app.AddressWard,
                    address_district = app.AddressDistrict,
                    phone = app.Phone,
                    email = app.Email,
                    training_cert_holder = app.TrainingCertHolder,
                    educational_qualification = app.EducationalQualification,
                    business_description = app.BusinessDescription,
                    payment_method = app.PaymentMethod,
                    payment_amount = app.PaymentAmount,
                    payment_confirmed = app.PaymentConfirmed,
                    payment_confirmed_by = app.PaymentConfirmedBy,
                    payment_confirmed_at = app.PaymentConfirmedAt,
                    assigned_akc_id = app.AssignedAkcId,
                    is_late_submission = app.IsLateSubmission,
                    late_fee_applicable = app.LateFeeApplicable,
                    submitted_at = app.SubmittedAt,
                    akc_reviewed_at = app.AkcReviewedAt,
                    revenue_checked_at = app.RevenueCheckedAt,
                    ppo_reviewed_at = app.PpoReviewedAt,
                    chief_approved_at = app.ChiefApprovedAt,
                    issued_at = app.IssuedAt,
                    created_at = app.CreatedAt,
                    updated_at = app.UpdatedAt,
                    applicant_name = app.ApplicantName,
                    applicant_mobile = app.ApplicantMobile,
                    applicant_account_email = app.ApplicantAccountEmail,
                    applicant_is_active = app.ApplicantIsActive,
                    applicant_is_verified = app.ApplicantIsVerified
                },
                documents = docs.Select(d => new {
                    id = d.Id,
                    application_id = d.ApplicationId,
                    doc_type = d.DocType,
                    original_filename = d.OriginalFilename,
                    stored_filename = d.StoredFilename,
                    file_path = d.FilePath,
                    file_size = d.FileSize,
                    mime_type = d.MimeType,
                    uploaded_at = d.UploadedAt
                }),
                workflow = workflow.Select(w => new {
                    action = w.Action, actor_name = w.ActorName,
                    actor_role = w.ActorRole, reason = w.Reason,
                    notes = w.Notes, created_at = w.CreatedAt
                }),
                license = license == null ? null : new {
                    id = license.Id,
                    uuid = license.Uuid,
                    license_number = license.LicenseNumber,
                    application_id = license.ApplicationId,
                    applicant_id = license.ApplicantId,
                    firm_name = license.FirmName,
                    seller_name = license.SellerName,
                    address = license.Address,
                    address_district = license.AddressDistrict,
                    issue_date = license.IssueDate,
                    expiry_date = license.ExpiryDate,
                    status = license.Status,
                    signed_by = license.SignedBy,
                    signed_at = license.SignedAt,
                    created_at = license.CreatedAt,
                    updated_at = license.UpdatedAt
                },
                checklist = checklist == null ? null : new {
                    id = checklist.Id,
                    application_id = checklist.ApplicationId,
                    a1 = checklist.A1RegisteredOnly,
                    a2 = checklist.A2NoOtherAlongside,
                    a3 = checklist.A3NoSimilarSubstances,
                    a4 = checklist.A4SeparateWarehouse,
                    a5 = checklist.A5LicenseDisplayed,
                    a6 = checklist.A6RegisteredListPosted,
                    b1 = checklist.B1NoMixedStorage,
                    b2 = checklist.B2LabelsReadable,
                    b3 = checklist.B3NoOpenContainers,
                    b4 = checklist.B4NotOpenedPunctured,
                    b5 = checklist.B5CleanPremises,
                    b6 = checklist.B6NoExpired,
                    b7 = checklist.B7NoUnregistered,
                    b8 = checklist.B8NoProhibited,
                    c1 = checklist.C1Precaution,
                    c2 = checklist.C2Precaution,
                    c3 = checklist.C3Precaution,
                    remarks = checklist.Remarks,
                    recommendation = checklist.Recommendation,
                    filled_at = checklist.FilledAt
                }
            });
        }

        [HttpPost("api/applications/{id:int}/action")]
        public async Task<IActionResult> ProcessAction(int id, [FromBody] ApplicationActionDto req)
        {
            using var conn = _connectionFactory.CreateConnection();
            var app = await conn.QuerySingleOrDefaultAsync<Application>(
                "SELECT * FROM Applications WHERE Id = @Id", new { Id = id });
            if (app == null) return NotFound(new { Error = "Not found" });

            var staffIdClaim = User.FindFirst("id")?.Value;
            int? staffId = staffIdClaim != null ? int.Parse(staffIdClaim) : null;
            var role = User.FindFirst("role")?.Value;

            string? newStatus = req.Action switch {
                // AKC flow
                "FORWARD_TO_ACCOUNTS"    => "REVENUE_CHECK",
                "AKC_APPROVE"            => "REVENUE_CHECK",
                "AKC_REJECT"             => "REJECTED",
                "AKC_RETURN"             => "RETURNED",
                // Accountant flow
                "ACCT_APPROVE"           => "PPO_REVIEW",
                "ACCT_REJECT"            => "REJECTED",
                "ACCT_RETURN"            => "RETURNED",
                // PPO flow
                "PPO_APPROVE"            => "CHIEF_APPROVAL",
                "PPO_REJECT"             => "REJECTED",
                "PPO_RETURN"             => "RETURNED",
                // Chief flow
                "APPROVE_AND_ISSUE"      => "ISSUED",
                "CHIEF_REJECT"           => "REJECTED",
                // Generic
                "REJECT"                 => "REJECTED",
                "RETURN"                 => "RETURNED",
                "RETURN_TO_APPLICANT"    => "RETURNED",
                _                        => null
            };

            if (newStatus == null) return BadRequest(new { Error = $"Unknown action: {req.Action}" });

            string? dbAction = req.Action switch {
                "FORWARD_TO_ACCOUNTS"    => "AKC_APPROVED",
                "AKC_APPROVE"            => "AKC_APPROVED",
                "AKC_REJECT"             => "AKC_REJECTED",
                "AKC_RETURN"             => "AKC_RETURNED",
                "ACCT_APPROVE"           => "ACCT_APPROVED",
                "ACCT_REJECT"            => "ACCT_REJECTED",
                "ACCT_RETURN"            => "ACCT_RETURNED",
                "PPO_APPROVE"            => "PPO_APPROVED",
                "PPO_REJECT"             => "PPO_REJECTED",
                "PPO_RETURN"             => "PPO_RETURNED",
                "APPROVE_AND_ISSUE"      => "LICENSE_ISSUED",
                "CHIEF_REJECT"           => "CHIEF_REJECTED",
                "REJECT"                 => "REJECTED",
                "RETURN"                 => "RETURNED",
                "RETURN_TO_APPLICANT"    => "RETURNED",
                _                        => req.Action
            };

            // Determine effective role for audit logging
            // If admin is performing, use the contextual role based on the action
            string effectiveRole = role ?? "";
            if (role == "ADMIN") {
                effectiveRole = req.Action switch {
                    "FORWARD_TO_ACCOUNTS" or "AKC_APPROVE" or "AKC_REJECT" or "AKC_RETURN" => "AKC_OFFICIAL",
                    "ACCT_APPROVE" or "ACCT_REJECT" or "ACCT_RETURN"                        => "ACCOUNTANT",
                    "PPO_APPROVE" or "PPO_REJECT" or "PPO_RETURN"                           => "PPO",
                    "APPROVE_AND_ISSUE" or "CHIEF_REJECT"                                  => "CHIEF",
                    _ => "ADMIN"
                };
            }

            // If Chief is approving and issuing, trigger the License Generator
            if (req.Action == "APPROVE_AND_ISSUE" && staffId.HasValue)
            {
                try
                {
                    // Ensure the status is set to CHIEF_APPROVAL first if it isn't already, 
                    // or just rely on the fact that PPO approval sets it.
                    // The generator service handles the transaction of creating a license and updating status.
                    var pdfPath = await _licenseGenerator.IssueLicenseAsync(id, staffId.Value);
                    return Ok(new { Message = "License issued successfully", NewStatus = "ISSUED", PdfUrl = pdfPath });
                }
                catch (Exception ex)
                {
                    return BadRequest(new { Error = $"License generation failed: {ex.Message}" });
                }
            }

            await conn.ExecuteAsync(
                "UPDATE Applications SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Status = newStatus, Id = id });

            await conn.ExecuteAsync(@"
                INSERT INTO WorkflowActions
                    (ApplicationId, ActorId, ActorRole, Action, Reason, CreatedAt)
                VALUES (@AppId, @ActorId, @Role, @Action, @Reason, GETUTCDATE())",
                new { AppId = id, ActorId = staffId, Role = effectiveRole, Action = dbAction, Reason = req.Reason ?? "" });

            // Save Checklist if provided (AKC role or Admin acting as AKC)
            if ((role == "AKC_OFFICIAL" || role == "ADMIN") && !string.IsNullOrEmpty(req.Checklist))
            {
                try {
                    var items = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(req.Checklist);
                    if (items != null)
                    {
                        await conn.ExecuteAsync(@"
                            IF EXISTS (SELECT 1 FROM ChecklistResponses WHERE ApplicationId = @AppId)
                                UPDATE ChecklistResponses SET 
                                    A1RegisteredOnly = @a1, A2NoOtherAlongside = @a2, A3NoSimilarSubstances = @a3, 
                                    A4SeparateWarehouse = @a4, A5LicenseDisplayed = @a5, A6RegisteredListPosted = @a6,
                                    B1NoMixedStorage = @b1, B2LabelsReadable = @b2, B3NoOpenContainers = @b3, 
                                    B4NotOpenedPunctured = @b4, B5CleanPremises = @b5, B6NoExpired = @b6, 
                                    B7NoUnregistered = @b7, B8NoProhibited = @b8,
                                    C1Precaution = @c1, C2Precaution = @c2, C3Precaution = @c3,
                                    Remarks = @Remarks, Recommendation = @Rec, FilledAt = GETUTCDATE()
                                WHERE ApplicationId = @AppId
                            ELSE
                                INSERT INTO ChecklistResponses 
                                    (ApplicationId, FilledBy, A1RegisteredOnly, A2NoOtherAlongside, A3NoSimilarSubstances, A4SeparateWarehouse, A5LicenseDisplayed, A6RegisteredListPosted, B1NoMixedStorage, B2LabelsReadable, B3NoOpenContainers, B4NotOpenedPunctured, B5CleanPremises, B6NoExpired, B7NoUnregistered, B8NoProhibited, C1Precaution, C2Precaution, C3Precaution, Remarks, Recommendation)
                                VALUES (@AppId, @ActorId, @a1, @a2, @a3, @a4, @a5, @a6, @b1, @b2, @b3, @b4, @b5, @b6, @b7, @b8, @c1, @c2, @c3, @Remarks, @Rec)",
                            new { 
                                AppId = id, ActorId = staffId,
                                a1 = Val(items, "a1"), a2 = Val(items, "a2"), a3 = Val(items, "a3"), a4 = Val(items, "a4"), a5 = Val(items, "a5"), a6 = Val(items, "a6"),
                                b1 = Val(items, "b1"), b2 = Val(items, "b2"), b3 = Val(items, "b3"), b4 = Val(items, "b4"), b5 = Val(items, "b5"), b6 = Val(items, "b6"), b7 = Val(items, "b7"), b8 = Val(items, "b8"),
                                c1 = ValStr(items, "c1"), c2 = ValStr(items, "c2"), c3 = ValStr(items, "c3"),
                                Remarks = req.Reason, Rec = req.Action.Contains("APPROVE") || req.Action.Contains("FORWARD") ? "APPROVE" : req.Action.Contains("REJECT") ? "REJECT" : "RETURN"
                            });
                    }
                } catch {}
            }

            // Insert AuditLog
            await conn.ExecuteAsync(@"
                INSERT INTO AuditLogs (ActorType, ActorId, ActorRole, Action, EntityType, EntityId, CreatedAt)
                VALUES ('STAFF', @ActorId, @Role, @Action, 'Application', @AppId, GETUTCDATE())",
                new { ActorId = staffId, Role = effectiveRole, Action = dbAction, AppId = id });

            return Ok(new { Message = "Action processed", NewStatus = newStatus });
        }

        [HttpPut("api/applications/{id:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> UpdateApplication(int id, [FromBody] ApplicationUpdateDto req)
        {
            using var conn = _connectionFactory.CreateConnection();
            var app = await conn.QuerySingleOrDefaultAsync("SELECT * FROM Applications WHERE Id = @Id", new { Id = id });
            if (app == null) return NotFound(new { Error = "Application not found" });

            var updates = new List<string>();
            var p = new DynamicParameters();
            p.Add("Id", id);

            if (req.FirmName != null) { updates.Add("FirmName = @FirmName"); p.Add("FirmName", req.FirmName); }
            if (req.AuthorizedPerson != null) { updates.Add("AuthorizedPerson = @AuthPerson"); p.Add("AuthPerson", req.AuthorizedPerson); }
            if (req.AddressDistrict != null) { updates.Add("AddressDistrict = @Dist"); p.Add("Dist", req.AddressDistrict); }
            if (req.AddressGapaNapa != null) { updates.Add("AddressGapaNapa = @Gapa"); p.Add("Gapa", req.AddressGapaNapa); }
            if (req.AddressWard != null) { updates.Add("AddressWard = @Ward"); p.Add("Ward", req.AddressWard); }
            if (req.Phone != null) { updates.Add("Phone = @Phone"); p.Add("Phone", req.Phone); }
            if (req.Email != null) { updates.Add("Email = @Email"); p.Add("Email", req.Email); }
            if (req.RegistrationNumber != null) { updates.Add("RegistrationNumber = @RegNum"); p.Add("RegNum", req.RegistrationNumber); }
            if (req.PanVatNumber != null) { updates.Add("PanVatNumber = @Pan"); p.Add("Pan", req.PanVatNumber); }
            if (req.LicenseCategory != null) { updates.Add("LicenseCategory = @Cat"); p.Add("Cat", req.LicenseCategory); }
            if (req.AssignedAkcId.HasValue) { updates.Add("AssignedAkcId = @AkcId"); p.Add("AkcId", req.AssignedAkcId.Value); }

            if (updates.Count == 0) return BadRequest("No fields to update");

            string sql = $"UPDATE Applications SET {string.Join(", ", updates)}, UpdatedAt = GETUTCDATE() WHERE Id = @Id";
            await conn.ExecuteAsync(sql, p);

            // Log Audit
            var staffId = int.Parse(User.FindFirst("id")?.Value ?? "0");
            await conn.ExecuteAsync(@"
                INSERT INTO AuditLogs (ActorType, ActorId, ActorRole, Action, EntityType, EntityId, CreatedAt)
                VALUES ('STAFF', @ActorId, 'ADMIN', 'ADMIN_EDIT_APP', 'Application', @Id, GETUTCDATE())",
                new { ActorId = staffId, Id = id });

            return Ok(new { Message = "Application updated successfully" });
        }

        private static int? Val(Dictionary<string, object> d, string k) => d.ContainsKey(k) ? (int.TryParse(d[k]?.ToString(), out int i) ? (int?)i : null) : null;
        private static string? ValStr(Dictionary<string, object> d, string k) => d.ContainsKey(k) ? d[k]?.ToString() : null;
    }

    public class ApplicationActionDto
    {
        public string Action { get; set; } = "";
        public string? Reason { get; set; }
        public bool? IsApproved { get; set; }
        public string? Checklist { get; set; }
    }

    public class ApplicationUpdateDto
    {
        public string? FirmName { get; set; }
        public string? AuthorizedPerson { get; set; }
        public string? AddressDistrict { get; set; }
        public string? AddressGapaNapa { get; set; }
        public string? AddressWard { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? PanVatNumber { get; set; }
        public string? LicenseCategory { get; set; }
        public int? AssignedAkcId { get; set; }
    }

    // ============================================================
    // REPORTS: GET /api/reports/dashboard, /forecast, /applications
    // ============================================================
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
                    SUM(CASE WHEN Status='SUBMITTED' THEN 1 ELSE 0 END) AS Submitted,
                    SUM(CASE WHEN Status='AKC_REVIEW' THEN 1 ELSE 0 END) AS AkcReview,
                    SUM(CASE WHEN Status='REVENUE_CHECK' THEN 1 ELSE 0 END) AS RevenueCheck,
                    SUM(CASE WHEN Status='PPO_REVIEW' THEN 1 ELSE 0 END) AS PpoReview,
                    SUM(CASE WHEN Status='CHIEF_APPROVAL' THEN 1 ELSE 0 END) AS ChiefApproval,
                    SUM(CASE WHEN Status='ISSUED' THEN 1 ELSE 0 END) AS Issued,
                    SUM(CASE WHEN Status='REJECTED' THEN 1 ELSE 0 END) AS Rejected
                FROM Applications" + appFilter, p);

            var licenseStats = await conn.QuerySingleAsync(@"
                SELECT
                    SUM(CASE WHEN l.Status='ACTIVE' THEN 1 ELSE 0 END) AS Active,
                    SUM(CASE WHEN l.Status='SUSPENDED' THEN 1 ELSE 0 END) AS Suspended,
                    SUM(CASE WHEN l.ExpiryDate BETWEEN GETUTCDATE() AND DATEADD(DAY, 30, GETUTCDATE()) THEN 1 ELSE 0 END) AS Expiring30Days
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
