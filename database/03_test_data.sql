USE PalmsDb;
GO

DECLARE @ApplicantId INT = (SELECT TOP 1 Id FROM Applicants WHERE Mobile = '9841000001');

IF @ApplicantId IS NOT NULL
BEGIN
    PRINT 'Found ApplicantId: ' + CAST(@ApplicantId AS VARCHAR);

    -- Insert Test Application 1
    DECLARE @App1Id INT;
    IF NOT EXISTS (SELECT 1 FROM Applications WHERE ReferenceNumber = 'APP-TEST-001')
    BEGIN
        INSERT INTO Applications (
            ReferenceNumber, ApplicantId, ApplicationType, Status,
            FirmName, RegistrationNumber, PanVatNumber, AuthorizedPerson,
            CitizenshipNumber, Designation, AddressGapaNapa, AddressWard, AddressDistrict,
            Phone, Email, TrainingCertHolder, EducationalQualification, BusinessDescription,
            PaymentMethod, PaymentAmount, PaymentConfirmed,
            AssignedAkcId, SubmittedAt
        ) VALUES (
            'APP-TEST-001', @ApplicantId, 'NEW', 'SUBMITTED',
            N'Janaki Krishi Sewa Kendra', N'REG-2080-001', N'123456789', N'Krishna Bahadur Mahato',
            N'27-01-77-02345', N'Proprietor', N'Janakpur', N'4', N'Dhanusha',
            '9841000001', 'demo@applicant.com', N'Yes', N'B.Sc. Agriculture', N'Retail agrovet business.',
            'BANK_VOUCHER', 2500.00, 1,
            (SELECT Id FROM Akcs WHERE District = 'Dhanusha'),
            GETUTCDATE()
        );
        SET @App1Id = SCOPE_IDENTITY();
        PRINT 'Test Application 1 created.';

        -- Add dummy document
        INSERT INTO ApplicationDocuments (ApplicationId, DocType, OriginalFilename, StoredFilename, FilePath, UploadedAt)
        VALUES (@App1Id, 'FIRM_REGISTRATION', 'reg_cert.pdf', 'reg_cert.pdf', '/uploads/reg_cert.pdf', GETUTCDATE());

        -- Add workflow steps
        INSERT INTO WorkflowActions (ApplicationId, ActorId, ActorRole, Action, Reason, CreatedAt)
        VALUES (@App1Id, NULL, 'APPLICANT', 'SUBMITTED', 'Initial submission', GETUTCDATE());
    END

    -- Insert Test Application 2
    DECLARE @App2Id INT;
    IF NOT EXISTS (SELECT 1 FROM Applications WHERE ReferenceNumber = 'APP-TEST-002')
    BEGIN
        INSERT INTO Applications (
            ReferenceNumber, ApplicantId, ApplicationType, Status,
            FirmName, RegistrationNumber, PanVatNumber, AuthorizedPerson,
            CitizenshipNumber, Designation, AddressGapaNapa, AddressWard, AddressDistrict,
            Phone, Email, TrainingCertHolder, EducationalQualification, BusinessDescription,
            PaymentMethod, PaymentAmount, PaymentConfirmed,
            AssignedAkcId, SubmittedAt
        ) VALUES (
            'APP-TEST-002', @ApplicantId, 'RENEWAL', 'SUBMITTED',
            N'Ram Agro Pvt. Ltd.', N'REG-2070-555', N'987654321', N'Ram Kumar Yadav',
            N'11-22-33-444', N'Director', N'Godaita', N'2', N'Sarlahi',
            '9800000002', 'ram@agro.com', N'Yes', N'B.Sc. Ag', N'Wholesale fertilizers and seeds.',
            'BANK_VOUCHER', 5000.00, 0,
            (SELECT Id FROM Akcs WHERE District = 'Sarlahi'),
            GETUTCDATE()
        );
        SET @App2Id = SCOPE_IDENTITY();
        PRINT 'Test Application 2 created.';
        
        -- Add workflow steps
        INSERT INTO WorkflowActions (ApplicationId, ActorId, ActorRole, Action, Reason, CreatedAt)
        VALUES (@App2Id, NULL, 'APPLICANT', 'SUBMITTED', 'Renewal request', GETUTCDATE());
    END
END
ELSE
BEGIN
    PRINT 'Demo Applicant not found. Please ensure 02_seed.sql was run.';
END
GO
