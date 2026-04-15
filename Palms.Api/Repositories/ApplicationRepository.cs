using Dapper;
using Palms.Api.Data;
using Palms.Api.Models.DTOs;
using Palms.Api.Models.Entities;

namespace Palms.Api.Repositories
{
    public class ApplicationRepository : IApplicationRepository
    {
        private readonly IDbConnectionFactory _connFactory;

        public ApplicationRepository(IDbConnectionFactory connFactory)
        {
            _connFactory = connFactory;
        }

        public async Task<int> CreateApplicationAsync(Application app)
        {
            using var conn = _connFactory.CreateConnection();
            
            // Generate simple REF number LIC-YYYYMMDD-XXXX
            // In a real app this would use a Sequence or stored proc to avoid race conditions.
            string refNum = $"REF-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000,9999)}";
            app.ReferenceNumber = refNum;

            string sql = @"
                INSERT INTO Applications (
                    ReferenceNumber, ApplicantId, ApplicationType, LicenseCategory, Status, FirmName, 
                    AuthorizedPerson, AddressDistrict, CreatedAt, UpdatedAt
                ) VALUES (
                    @ReferenceNumber, @ApplicantId, @ApplicationType, @LicenseCategory, @Status, @FirmName,
                    @AuthorizedPerson, @AddressDistrict, GETUTCDATE(), GETUTCDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() as int);";

            var id = await conn.ExecuteScalarAsync<int>(sql, app);
            
            // Log submission
            await LogActionInternalAsync(conn, id, null, "APPLICANT", "SUBMITTED", "Initial submission");
            
            return id;
        }

        public async Task<Application?> GetApplicationByRefAsync(string referenceNumber)
        {
            using var conn = _connFactory.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Application>(
                "SELECT * FROM Applications WHERE ReferenceNumber = @Ref", new { Ref = referenceNumber });
        }

        public async Task<Application?> GetApplicationByIdAsync(int id)
        {
            using var conn = _connFactory.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Application>(
                "SELECT * FROM Applications WHERE Id = @Id", new { Id = id });
        }

        public async Task<IEnumerable<ApplicationStatusDto>> GetApplicationsForApplicantAsync(int applicantId)
        {
            using var conn = _connFactory.CreateConnection();
            string sql = @"
                SELECT a.Id, a.ReferenceNumber, a.Status, a.FirmName, a.ApplicationType, a.LicenseCategory, a.CreatedAt,
                       l.LicenseNumber, l.PdfPath
                FROM Applications a
                LEFT JOIN Licenses l ON a.Id = l.ApplicationId
                WHERE a.ApplicantId = @Id 
                ORDER BY a.CreatedAt DESC";
            return await conn.QueryAsync<ApplicationStatusDto>(sql, new { Id = applicantId });
        }

        public async Task<IEnumerable<ApplicationStatusDto>> GetApplicationsByAkcAndStatusAsync(int akcId, string status)
        {
            using var conn = _connFactory.CreateConnection();
            string sql = @"
                SELECT a.Id, a.ReferenceNumber, a.Status, a.FirmName, a.ApplicationType, a.LicenseCategory, a.CreatedAt,
                       l.LicenseNumber, l.PdfPath
                FROM Applications a
                LEFT JOIN Licenses l ON a.Id = l.ApplicationId
                WHERE a.AssignedAkcId = @AkcId AND a.Status = @Status 
                ORDER BY a.CreatedAt ASC";
            return await conn.QueryAsync<ApplicationStatusDto>(sql, new { AkcId = akcId, Status = status });
        }

        public async Task<IEnumerable<ApplicationStatusDto>> GetApplicationsByStatusAsync(string status)
        {
            using var conn = _connFactory.CreateConnection();
            string sql = @"
                SELECT a.Id, a.ReferenceNumber, a.Status, a.FirmName, a.ApplicationType, a.LicenseCategory, a.CreatedAt,
                       l.LicenseNumber, l.PdfPath
                FROM Applications a
                LEFT JOIN Licenses l ON a.Id = l.ApplicationId
                WHERE a.Status = @Status 
                ORDER BY a.CreatedAt ASC";
            return await conn.QueryAsync<ApplicationStatusDto>(sql, new { Status = status });
        }

        public async Task UpdateStatusAsync(int id, string newStatus, string? actorRole = null, DateTime? timestamp = null)
        {
            using var conn = _connFactory.CreateConnection();
            
            var parameters = new DynamicParameters();
            parameters.Add("@Id", id);
            parameters.Add("@Status", newStatus);
            
            string updateQuery = "UPDATE Applications SET Status = @Status, UpdatedAt = GETUTCDATE()";
            
            // Set timestamp based on role
            if (actorRole == "AKC_OFFICIAL" && timestamp.HasValue)
            {
                updateQuery += ", AkcReviewedAt = @Timestamp";
                parameters.Add("@Timestamp", timestamp.Value);
            }
            else if (actorRole == "PPO" && timestamp.HasValue)
            {
                updateQuery += ", PpoReviewedAt = @Timestamp";
                parameters.Add("@Timestamp", timestamp.Value);
            }
            else if (actorRole == "CHIEF" && timestamp.HasValue)
            {
                updateQuery += ", ChiefApprovedAt = @Timestamp";
                parameters.Add("@Timestamp", timestamp.Value);
            }
            
            updateQuery += " WHERE Id = @Id";
            await conn.ExecuteAsync(updateQuery, parameters);
        }

        public async Task LogActionAsync(int applicationId, int? actorId, string actorRole, string action, string? notes)
        {
            using var conn = _connFactory.CreateConnection();
            await LogActionInternalAsync(conn, applicationId, actorId, actorRole, action, notes);
        }

        private async Task LogActionInternalAsync(System.Data.IDbConnection conn, int appId, int? actorId, string role, string action, string? notes)
        {
            string sql = @"
                INSERT INTO WorkflowActions (ApplicationId, ActorId, ActorRole, Action, Notes, CreatedAt)
                VALUES (@AppId, @ActorId, @Role, @Action, @Notes, GETUTCDATE())";
            await conn.ExecuteAsync(sql, new { AppId = appId, ActorId = actorId, Role = role, Action = action, Notes = notes });
        }

        public async Task AddDocumentAsync(ApplicationDocument doc)
        {
            using var conn = _connFactory.CreateConnection();
            string sql = @"
                INSERT INTO ApplicationDocuments (
                    ApplicationId, DocType, OriginalFilename, StoredFilename, FilePath, FileSize, MimeType, UploadedAt
                ) VALUES (
                    @ApplicationId, @DocType, @OriginalFilename, @StoredFilename, @FilePath, @FileSize, @MimeType, GETUTCDATE()
                );";
            await conn.ExecuteAsync(sql, doc);
        }
    }
}
