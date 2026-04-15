using Dapper;
using Palms.Api.Data;
using Palms.Api.Models.Entities;

using Palms.Api.Models.DTOs;

namespace Palms.Api.Repositories
{
    public class ApplicantRepository : IApplicantRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ApplicantRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Applicant?> GetByMobileAsync(string mobile)
        {
            using var conn = _connectionFactory.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Applicant>(
                "SELECT * FROM Applicants WHERE Mobile = @Mobile", new { Mobile = mobile });
        }

        public async Task<Applicant?> GetByEmailAsync(string email)
        {
            using var conn = _connectionFactory.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Applicant>(
                "SELECT * FROM Applicants WHERE Email = @Email", new { Email = email });
        }

        public async Task<Applicant?> GetByMobileOrEmailAsync(string identifier)
        {
            using var conn = _connectionFactory.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Applicant>(
                "SELECT * FROM Applicants WHERE Mobile = @Id OR Email = @Id", new { Id = identifier });
        }

        public async Task<Applicant?> GetByIdAsync(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Applicant>(
                "SELECT * FROM Applicants WHERE Id = @Id", new { Id = id });
        }

        public async Task<int> CreateApplicantAsync(Applicant app)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"
                INSERT INTO Applicants (Mobile, PasswordHash, FullName, IsVerified, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Mobile, @PasswordHash, @FullName, @IsVerified, @IsActive, GETUTCDATE(), GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() as int);";
                
            var id = await conn.ExecuteScalarAsync<int>(sql, app);
            
            string profileSql = @"
                INSERT INTO ApplicantProfiles (ApplicantId, AuthorizedPersonName, Phone, CreatedAt, UpdatedAt)
                VALUES (@Id, @FullName, @Mobile, GETUTCDATE(), GETUTCDATE());";
                
            await conn.ExecuteAsync(profileSql, new { Id = id, app.FullName, app.Mobile });
            
            return id;
        }

        public async Task UpdateVerificationStatusAsync(int id, bool isVerified)
        {
            using var conn = _connectionFactory.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE Applicants SET IsVerified = @IsVerified, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Id = id, IsVerified = isVerified });
        }

        public async Task SaveOtpAsync(int applicantId, string mobile, string otpHash, DateTime expiresAt, string purpose = "REGISTRATION")
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"
                INSERT INTO OtpVerifications (ApplicantId, Mobile, OtpHash, Purpose, ExpiresAt, IsUsed, Attempts, CreatedAt)
                VALUES (@ApplicantId, @Mobile, @OtpHash, @Purpose, @ExpiresAt, 0, 0, GETUTCDATE());";
            await conn.ExecuteAsync(sql, new { ApplicantId = applicantId, Mobile = mobile, OtpHash = otpHash, ExpiresAt = expiresAt, Purpose = purpose });
        }

        public async Task<dynamic?> GetLatestOtpAsync(string mobile)
        {
            using var conn = _connectionFactory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync(
                "SELECT TOP 1 * FROM OtpVerifications WHERE Mobile = @Mobile ORDER BY CreatedAt DESC",
                new { Mobile = mobile });
        }

        public async Task<dynamic?> GetLatestOtpByPurposeAsync(string mobile, string purpose)
        {
            using var conn = _connectionFactory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync(
                "SELECT TOP 1 * FROM OtpVerifications WHERE Mobile = @Mobile AND Purpose = @Purpose ORDER BY CreatedAt DESC",
                new { Mobile = mobile, Purpose = purpose });
        }

        public async Task MarkOtpAsUsedAsync(int otpId)
        {
            using var conn = _connectionFactory.CreateConnection();
            await conn.ExecuteAsync("UPDATE OtpVerifications SET IsUsed = 1 WHERE Id = @Id", new { Id = otpId });
        }

        public async Task UpdateEmailAsync(int id, string email)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "UPDATE Applicants SET Email = @Email, UpdatedAt = GETUTCDATE() WHERE Id = @Id";
            await conn.ExecuteAsync(sql, new { Email = email, Id = id });
        }

        public async Task UpdateMobileAsync(int id, string mobile)
        {
            using var conn = _connectionFactory.CreateConnection();
            // Update mobile in both Applicants (auth) and Profiles (data)
            string sql = @"
                UPDATE Applicants SET Mobile = @Mobile, UpdatedAt = GETUTCDATE() WHERE Id = @Id;
                UPDATE ApplicantProfiles SET Phone = @Mobile, UpdatedAt = GETUTCDATE() WHERE ApplicantId = @Id;
            ";
            await conn.ExecuteAsync(sql, new { Mobile = mobile, Id = id });
        }

        public async Task ResetPasswordAsync(int id, string newHash)
        {
            using var conn = _connectionFactory.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE Applicants SET PasswordHash = @Hash, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Hash = newHash, Id = id });
        }

        public async Task<Palms.Api.Models.DTOs.ApplicantProfileDto?> GetApplicantProfileAsync(int applicantId)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"
                SELECT a.Id, a.Mobile, a.FullName, a.IsVerified, 
                       p.FirmName, p.AddressDistrict, p.ProfileComplete
                FROM Applicants a
                LEFT JOIN ApplicantProfiles p ON a.Id = p.ApplicantId
                WHERE a.Id = @Id";
            return await conn.QuerySingleOrDefaultAsync<ApplicantProfileDto>(sql, new { Id = applicantId });
        }
    }
}
