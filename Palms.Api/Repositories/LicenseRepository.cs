using Dapper;
using Palms.Api.Data;
using Palms.Api.Models.Entities;

namespace Palms.Api.Repositories
{
    public class LicenseRepository : ILicenseRepository
    {
        private readonly IDbConnectionFactory _connFactory;

        public LicenseRepository(IDbConnectionFactory connFactory)
        {
            _connFactory = connFactory;
        }

        public async Task<int> CreateLicenseAsync(License license)
        {
            using var conn = _connFactory.CreateConnection();
            string sql = @"
                INSERT INTO Licenses (
                    LicenseNumber, ApplicationId, ApplicantId, FirmName, 
                    SellerName, Address, AddressDistrict, IssueDate, 
                    ExpiryDate, Status, QrCodeData, SignedBy, SignedAt, CreatedAt, UpdatedAt
                ) VALUES (
                    @LicenseNumber, @ApplicationId, @ApplicantId, @FirmName,
                    @SellerName, @Address, @AddressDistrict, @IssueDate,
                    @ExpiryDate, @Status, @QrCodeData, @SignedBy, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() as int);";

            return await conn.ExecuteScalarAsync<int>(sql, license);
        }

        public async Task<License?> GetByLicenseNumberAsync(string licenseNumber)
        {
            using var conn = _connFactory.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<License>(
                "SELECT * FROM Licenses WHERE LicenseNumber = @Ln", new { Ln = licenseNumber });
        }

        public async Task<Palms.Api.Models.DTOs.LicenseDetailDto?> GetLicenseDetailByNumberAsync(string licenseNumber)
        {
            using var conn = _connFactory.CreateConnection();
            string sql = @"
                SELECT 
                    l.LicenseNumber,
                    a.LicenseCategory,
                    l.FirmName,
                    a.RegistrationNumber,
                    a.PanVatNumber,
                    a.AuthorizedPerson,
                    a.CitizenshipNumber,
                    a.Designation,
                    a.AddressDistrict,
                    a.AddressGapaNapa,
                    a.AddressWard,
                    a.Phone,
                    a.BusinessDescription,
                    CONVERT(VARCHAR, l.ExpiryDate, 23) as ExpiryDate
                FROM Licenses l
                JOIN Applications a ON l.ApplicationId = a.Id
                WHERE l.LicenseNumber = @Ln";
                
            return await conn.QuerySingleOrDefaultAsync<Palms.Api.Models.DTOs.LicenseDetailDto>(sql, new { Ln = licenseNumber });
        }

        public async Task<IEnumerable<License>> GetAllLicensesAsync()
        {
            using var conn = _connFactory.CreateConnection();
            return await conn.QueryAsync<License>(
                "SELECT * FROM Licenses ORDER BY IssueDate DESC");
        }

        public async Task<IEnumerable<License>> GetLicensesByApplicantIdAsync(int applicantId)
        {
            using var conn = _connFactory.CreateConnection();
            return await conn.QueryAsync<License>(
                "SELECT * FROM Licenses WHERE ApplicantId = @Aid ORDER BY ExpiryDate DESC",
                new { Aid = applicantId });
        }

        public async Task UpdatePdfPathAsync(int id, string pdfPath)
        {
             using var conn = _connFactory.CreateConnection();
             await conn.ExecuteAsync(
                 "UPDATE Licenses SET PdfPath = @Path, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                 new { Path = pdfPath, Id = id });
        }

        public async Task UpdateLicenseRenewalAsync(License license)
        {
            using var conn = _connFactory.CreateConnection();
            string sql = @"
                UPDATE Licenses SET 
                    ApplicationId = @ApplicationId,
                    FirmName = @FirmName,
                    SellerName = @SellerName,
                    Address = @Address,
                    AddressDistrict = @AddressDistrict,
                    IssueDate = @IssueDate,
                    ExpiryDate = @ExpiryDate,
                    QrCodeData = @QrCodeData,
                    SignedBy = @SignedBy,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id;";

            await conn.ExecuteAsync(sql, license);
        }
    }
}
