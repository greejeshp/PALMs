using Palms.Api.Models.Entities;
using Palms.Api.Repositories;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Palms.Api.Services
{
    public class LicenseGeneratorService
    {
        private readonly ILicenseRepository _licenseRepo;
        private readonly IApplicationRepository _appRepo;
        private readonly IWebHostEnvironment _env;

        public LicenseGeneratorService(ILicenseRepository licenseRepo, IApplicationRepository appRepo, IWebHostEnvironment env)
        {
            _licenseRepo = licenseRepo;
            _appRepo = appRepo;
            _env = env;
            // Configure QuestPDF license (Community is free for small companies, but required to silence warnings)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string> IssueLicenseAsync(int applicationId, int chiefId)
        {
            var app = await _appRepo.GetApplicationByIdAsync(applicationId);
            if (app == null || app.Status != "CHIEF_APPROVAL")
                throw new Exception("Application not ready for license generation.");

            bool isRenewal = app.ApplicationType == "RENEWAL" && !string.IsNullOrEmpty(app.PriorLicenseNumber);
            string licenseNum;

            if (isRenewal)
            {
                licenseNum = app.PriorLicenseNumber;
            }
            else
            {
                // Generate License Number Safely
                string distPart = app.AddressDistrict ?? "UNK";
                string distCode = distPart.Length >= 3 ? distPart.Substring(0, 3).ToUpper() : distPart.ToUpper().PadRight(3, 'X');
                string year = DateTime.UtcNow.Year.ToString();
                string seq = new Random().Next(1000, 9999).ToString();
                licenseNum = $"LIC-{distCode}-{year}-{seq}";
            }

            // Generate QR Code containing verification URL
            string qrData = $"https://palms.gov.np/verify?license={licenseNum}";
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(20);

            var issueDate = DateTime.UtcNow.Date;
            var expiryDate = issueDate.AddYears(1);

            int licenseId;
            var existingLicense = isRenewal ? await _licenseRepo.GetByLicenseNumberAsync(licenseNum) : null;

            if (isRenewal && existingLicense != null)
            {
                existingLicense.IssueDate = issueDate;
                existingLicense.ExpiryDate = expiryDate;
                existingLicense.ApplicationId = app.Id;
                existingLicense.SignedBy = chiefId;
                existingLicense.FirmName = app.FirmName;
                existingLicense.SellerName = app.AuthorizedPerson;
                existingLicense.Address = $"{app.AddressGapaNapa}-{app.AddressWard}";
                existingLicense.AddressDistrict = app.AddressDistrict;
                existingLicense.QrCodeData = qrData;

                await _licenseRepo.UpdateLicenseRenewalAsync(existingLicense);
                licenseId = existingLicense.Id;
            }
            else
            {
                var license = new License
                {
                    LicenseNumber = licenseNum,
                    ApplicationId = app.Id,
                    ApplicantId = app.ApplicantId,
                    FirmName = app.FirmName,
                    SellerName = app.AuthorizedPerson,
                    Address = $"{app.AddressGapaNapa}-{app.AddressWard}",
                    AddressDistrict = app.AddressDistrict,
                    IssueDate = issueDate,
                    ExpiryDate = expiryDate,
                    QrCodeData = qrData,
                    SignedBy = chiefId
                };

                licenseId = await _licenseRepo.CreateLicenseAsync(license);
                existingLicense = license;
            }
            
            // Build PDF
            string pdfDirectory = Path.Combine(_env.WebRootPath ?? "wwwroot", "licenses");
            Directory.CreateDirectory(pdfDirectory);
            string pdfFilename = $"{licenseNum}.pdf";
            string pdfPath = Path.Combine(pdfDirectory, pdfFilename);

            GeneratePdf(existingLicense, qrCodeImage, pdfPath);
            
            // Update db with path
            await _licenseRepo.UpdatePdfPathAsync(licenseId, $"/licenses/{pdfFilename}");
            
            // Update application status
            await _appRepo.UpdateStatusAsync(app.Id, "ISSUED", "SYSTEM", DateTime.UtcNow);
            await _appRepo.LogActionAsync(app.Id, chiefId, "CHIEF", "LICENSE_ISSUED", $"License {licenseNum} generated.");

            return $"/licenses/{pdfFilename}";
        }

        private void GeneratePdf(License license, byte[] qrCodeImage, string destinationPath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(x => ComposeContent(x, license, qrCodeImage));
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(destinationPath);
        }

        private void ComposeHeader(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("Directorate of Agriculture Development").FontSize(20).SemiBold().AlignCenter();
                column.Item().Text("Madhesh Province, Nepal").FontSize(14).AlignCenter();
                column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            });
        }

        private void ComposeContent(IContainer container, License license, byte[] qrCodeImage)
        {
            container.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Spacing(20);

                column.Item().Text("PESTICIDE SELLER LICENSE").FontSize(18).Bold().AlignCenter();
                column.Item().Text($"License Number: {license.LicenseNumber}").FontSize(14).SemiBold().AlignCenter();

                column.Item().PaddingTop(25).Text("This is to certify that").FontSize(14);
                column.Item().Text(license.FirmName).FontSize(16).Bold();
                column.Item().Text($"Proprietor: {license.SellerName}").FontSize(14);
                column.Item().Text($"Address: {license.Address}, {license.AddressDistrict}").FontSize(14);
                
                column.Item().PaddingTop(20).Text("is authorized to sell and distribute registered pesticides in accordance with the Pesticide Management Act.").FontSize(12);

                column.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Issue Date: {license.IssueDate:yyyy-MM-dd}");
                        c.Item().Text($"Valid Until: {license.ExpiryDate:yyyy-MM-dd}").Bold();
                    });

                    row.ConstantItem(100).Image(qrCodeImage);
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text("Scan QR code to verify license authenticity.").FontSize(10).FontColor(Colors.Grey.Medium);
        }
    }
}
