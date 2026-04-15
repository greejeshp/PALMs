-- ============================================================
-- PALMS v2 — MS SQL Server 2016 Schema
-- Pesticide Applicator Licensing and Management System
-- Migrated from SQLite
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PalmsDb')
BEGIN
    CREATE DATABASE PalmsDb
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END
GO

USE PalmsDb;
GO

-- ============================================================
-- USERS (all staff roles)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
CREATE TABLE Users (
    Id              INT             IDENTITY(1,1)   PRIMARY KEY,
    Uuid            NVARCHAR(36)    NOT NULL        UNIQUE DEFAULT NEWID(),
    Username        NVARCHAR(100)   NOT NULL        UNIQUE,
    Email           NVARCHAR(255)   NOT NULL        UNIQUE,
    PasswordHash    NVARCHAR(500)   NOT NULL,
    FullName        NVARCHAR(255)   NOT NULL,
    Role            NVARCHAR(50)    NOT NULL        CHECK(Role IN ('ADMIN','AKC_OFFICIAL','PPO','ACCOUNTANT','CHIEF')),
    District        NVARCHAR(100)   NULL,
    AkcId           INT             NULL,
    IsActive        BIT             NOT NULL        DEFAULT 1,
    IsLocked        BIT             NOT NULL        DEFAULT 0,
    FailedLoginCount INT            NOT NULL        DEFAULT 0,
    LastLoginAt     DATETIME2       NULL,
    LastLoginIp     NVARCHAR(50)    NULL,
    CreatedAt       DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2       NOT NULL        DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- APPLICANTS (self-registered pesticide sellers)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Applicants')
CREATE TABLE Applicants (
    Id              INT             IDENTITY(1,1)   PRIMARY KEY,
    Uuid            NVARCHAR(36)    NOT NULL        UNIQUE DEFAULT NEWID(),
    Mobile          NVARCHAR(20)    NOT NULL        UNIQUE,
    Email           NVARCHAR(255)   NULL,
    PasswordHash    NVARCHAR(500)   NOT NULL,
    FullName        NVARCHAR(255)   NOT NULL,
    IsVerified      BIT             NOT NULL        DEFAULT 0,
    IsActive        BIT             NOT NULL        DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2       NOT NULL        DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- OTP VERIFICATIONS
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OtpVerifications')
CREATE TABLE OtpVerifications (
    Id              INT             IDENTITY(1,1)   PRIMARY KEY,
    ApplicantId     INT             NOT NULL,
    OtpHash         NVARCHAR(500)   NOT NULL,
    Mobile          NVARCHAR(20)    NOT NULL,
    Purpose         NVARCHAR(20)    NOT NULL        DEFAULT 'REGISTRATION'
                                    CHECK(Purpose IN ('REGISTRATION','LOGIN','RESET')),
    Attempts        INT             NOT NULL        DEFAULT 0,
    IsUsed          BIT             NOT NULL        DEFAULT 0,
    ExpiresAt       DATETIME2       NOT NULL,
    CreatedAt       DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    CONSTRAINT FK_OtpVerifications_Applicants FOREIGN KEY (ApplicantId) REFERENCES Applicants(Id)
);
GO

-- ============================================================
-- APPLICANT PROFILES (Anusuchi-1 fields)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApplicantProfiles')
CREATE TABLE ApplicantProfiles (
    Id                          INT             IDENTITY(1,1)   PRIMARY KEY,
    ApplicantId                 INT             NOT NULL        UNIQUE,
    FirmName                    NVARCHAR(255)   NULL,
    RegistrationNumber          NVARCHAR(100)   NULL,
    PanVatNumber                NVARCHAR(50)    NULL,
    LicenseNumber               NVARCHAR(100)   NULL,
    LicenseIssueDate            DATE            NULL,
    LicenseExpiryDate           DATE            NULL,
    AuthorizedPersonName        NVARCHAR(255)   NULL,
    CitizenshipNumber           NVARCHAR(50)    NULL,
    Designation                 NVARCHAR(100)   NULL,
    AddressGapaNapa             NVARCHAR(255)   NULL,
    AddressWard                 NVARCHAR(20)    NULL,
    AddressDistrict             NVARCHAR(100)   NULL,
    Phone                       NVARCHAR(20)    NULL,
    TrainingCertificateHolder   NVARCHAR(255)   NULL,
    EducationalQualification    NVARCHAR(255)   NULL,
    BusinessDescription         NVARCHAR(MAX)   NULL,
    ProfileComplete             BIT             NOT NULL        DEFAULT 0,
    CreatedAt                   DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    UpdatedAt                   DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ApplicantProfiles_Applicants FOREIGN KEY (ApplicantId) REFERENCES Applicants(Id)
);
GO

-- ============================================================
-- AGRICULTURE KNOWLEDGE CENTRES
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Akcs')
CREATE TABLE Akcs (
    Id          INT             IDENTITY(1,1)   PRIMARY KEY,
    Name        NVARCHAR(255)   NOT NULL,
    District    NVARCHAR(100)   NOT NULL,
    Areas       NVARCHAR(MAX)   NULL,   -- JSON array stored as NVARCHAR
    IsActive    BIT             NOT NULL        DEFAULT 1,
    CreatedAt   DATETIME2       NOT NULL        DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- APPLICATIONS
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Applications')
CREATE TABLE Applications (
    Id                      INT             IDENTITY(1,1)   PRIMARY KEY,
    Uuid                    NVARCHAR(36)    NOT NULL        UNIQUE DEFAULT NEWID(),
    ReferenceNumber         NVARCHAR(50)    NOT NULL        UNIQUE,
    ApplicantId             INT             NOT NULL,
    ApplicationType         NVARCHAR(10)    NOT NULL        DEFAULT 'NEW'
                                            CHECK(ApplicationType IN ('NEW','RENEWAL')),
    Status                  NVARCHAR(20)    NOT NULL        DEFAULT 'SUBMITTED'
                                            CHECK(Status IN (
                                                'DRAFT','SUBMITTED','AKC_REVIEW','REVENUE_CHECK',
                                                'PPO_REVIEW','CHIEF_APPROVAL','ISSUED',
                                                'REJECTED','RETURNED','CANCELLED'
                                            )),
    -- Anusuchi-1 snapshot
    FirmName                NVARCHAR(255)   NOT NULL,
    RegistrationNumber      NVARCHAR(100)   NULL,
    PanVatNumber            NVARCHAR(50)    NULL,
    PriorLicenseNumber      NVARCHAR(100)   NULL,
    PriorLicenseExpiry      DATE            NULL,
    AuthorizedPerson        NVARCHAR(255)   NOT NULL,
    CitizenshipNumber       NVARCHAR(50)    NULL,
    Designation             NVARCHAR(100)   NULL,
    AddressGapaNapa         NVARCHAR(255)   NULL,
    AddressWard             NVARCHAR(20)    NULL,
    AddressDistrict         NVARCHAR(100)   NOT NULL,
    Phone                   NVARCHAR(20)    NULL,
    Email                   NVARCHAR(255)   NULL,
    TrainingCertHolder      NVARCHAR(255)   NULL,
    EducationalQualification NVARCHAR(255)  NULL,
    BusinessDescription     NVARCHAR(MAX)   NULL,
    -- Payment
    PaymentMethod           NVARCHAR(20)    NULL
                                            CHECK(PaymentMethod IN ('BANK_VOUCHER','ONLINE','PENDING')),
    PaymentAmount           DECIMAL(10,2)   NULL,
    PaymentConfirmed        BIT             NOT NULL        DEFAULT 0,
    PaymentConfirmedBy      INT             NULL,
    PaymentConfirmedAt      DATETIME2       NULL,
    -- Routing
    AssignedAkcId           INT             NULL,
    IsLateSubmission        BIT             NOT NULL        DEFAULT 0,
    LateFeeApplicable       BIT             NOT NULL        DEFAULT 0,
    -- Timestamps
    SubmittedAt             DATETIME2       NULL,
    AkcReviewedAt           DATETIME2       NULL,
    RevenueCheckedAt        DATETIME2       NULL,
    PpoReviewedAt           DATETIME2       NULL,
    ChiefApprovedAt         DATETIME2       NULL,
    IssuedAt                DATETIME2       NULL,
    CreatedAt               DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    UpdatedAt               DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Applications_Applicants   FOREIGN KEY (ApplicantId)   REFERENCES Applicants(Id),
    CONSTRAINT FK_Applications_Akcs         FOREIGN KEY (AssignedAkcId) REFERENCES Akcs(Id)
);
GO

-- ============================================================
-- APPLICATION DOCUMENTS
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApplicationDocuments')
CREATE TABLE ApplicationDocuments (
    Id                  INT             IDENTITY(1,1)   PRIMARY KEY,
    ApplicationId       INT             NOT NULL,
    DocType             NVARCHAR(50)    NOT NULL
                                        CHECK(DocType IN (
                                            'EXISTING_LICENSE','TRANSACTION_VOLUME','AUDIT_REPORT',
                                            'PAYMENT_RECEIPT','TRAINING_CERTIFICATE','FIRM_REGISTRATION',
                                            'PAN_VAT_CERTIFICATE','BUSINESS_DESCRIPTION','OTHER'
                                        )),
    OriginalFilename    NVARCHAR(500)   NOT NULL,
    StoredFilename      NVARCHAR(500)   NOT NULL,
    FilePath            NVARCHAR(1000)  NOT NULL,
    FileSize            BIGINT          NULL,
    MimeType            NVARCHAR(100)   NULL,
    UploadedAt          DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    CONSTRAINT FK_AppDocuments_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id)
);
GO

-- ============================================================
-- CHECKLIST RESPONSES (Anusuchi-2 — AKC Inspector)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChecklistResponses')
CREATE TABLE ChecklistResponses (
    Id                      INT             IDENTITY(1,1)   PRIMARY KEY,
    ApplicationId           INT             NOT NULL,
    FilledBy                INT             NOT NULL,
    -- Section A
    A1RegisteredOnly        BIT             NULL,
    A2NoOtherAlongside      BIT             NULL,
    A3NoSimilarSubstances   BIT             NULL,
    A4SeparateWarehouse     BIT             NULL,
    A5LicenseDisplayed      BIT             NULL,
    A6RegisteredListPosted  BIT             NULL,
    -- Section B
    B1NoMixedStorage        BIT             NULL,
    B2LabelsReadable        BIT             NULL,
    B3NoOpenContainers      BIT             NULL,
    B4NotOpenedPunctured    BIT             NULL,
    B5CleanPremises         BIT             NULL,
    B6NoExpired             BIT             NULL,
    B7NoUnregistered        BIT             NULL,
    B8NoProhibited          BIT             NULL,
    BAdditionalObservations NVARCHAR(MAX)   NULL,
    -- Section C
    C1Precaution            NVARCHAR(MAX)   NULL,
    C2Precaution            NVARCHAR(MAX)   NULL,
    C3Precaution            NVARCHAR(MAX)   NULL,
    -- Decision
    Recommendation          NVARCHAR(10)    NULL        CHECK(Recommendation IN ('APPROVE','REJECT','RETURN')),
    Remarks                 NVARCHAR(MAX)   NULL,
    FilledAt                DATETIME2       NOT NULL    DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Checklist_Applications    FOREIGN KEY (ApplicationId) REFERENCES Applications(Id),
    CONSTRAINT FK_Checklist_Users           FOREIGN KEY (FilledBy)      REFERENCES Users(Id)
);
GO

-- ============================================================
-- WORKFLOW ACTIONS (immutable audit of every step)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowActions')
CREATE TABLE WorkflowActions (
    Id                  INT             IDENTITY(1,1)   PRIMARY KEY,
    ApplicationId       INT             NOT NULL,
    ActorId             INT             NULL,
    ActorApplicantId    INT             NULL,
    ActorRole           NVARCHAR(50)    NOT NULL,
    Action              NVARCHAR(50)    NOT NULL
                                        CHECK(Action IN (
                                            'SUBMITTED',
                                            'AKC_APPROVED','AKC_REJECTED','AKC_RETURNED',
                                            'PAYMENT_VERIFIED',
                                            'ACCT_APPROVED','ACCT_REJECTED','ACCT_RETURNED',
                                            'PPO_APPROVED','PPO_REJECTED','PPO_RETURNED','PPO_REQUESTED_DOCS',
                                            'CHIEF_APPROVED','CHIEF_REJECTED',
                                            'LICENSE_ISSUED',
                                            'REJECTED','RETURNED',
                                            'SUSPENDED','CANCELLED','REINSTATED','RESUBMITTED','SYSTEM'
                                        )),
    Reason              NVARCHAR(MAX)   NULL,
    Notes               NVARCHAR(MAX)   NULL,
    IpAddress           NVARCHAR(50)    NULL,
    CreatedAt           DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    CONSTRAINT FK_WorkflowActions_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id)
);
GO

-- ============================================================
-- LICENSES
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Licenses')
CREATE TABLE Licenses (
    Id                  INT             IDENTITY(1,1)   PRIMARY KEY,
    Uuid                NVARCHAR(36)    NOT NULL        UNIQUE DEFAULT NEWID(),
    LicenseNumber       NVARCHAR(100)   NOT NULL        UNIQUE,
    ApplicationId       INT             NOT NULL,
    ApplicantId         INT             NOT NULL,
    FirmName            NVARCHAR(255)   NOT NULL,
    SellerName          NVARCHAR(255)   NOT NULL,
    Address             NVARCHAR(500)   NOT NULL,
    AddressDistrict     NVARCHAR(100)   NULL,
    IssueDate           DATE            NOT NULL,
    ExpiryDate          DATE            NOT NULL,
    Status              NVARCHAR(20)    NOT NULL        DEFAULT 'ACTIVE'
                                        CHECK(Status IN ('ACTIVE','EXPIRED','SUSPENDED','CANCELLED')),
    QrCodeData          NVARCHAR(MAX)   NULL,
    PdfPath             NVARCHAR(1000)  NULL,
    SignedBy            INT             NULL,
    SignedAt            DATETIME2       NULL,
    SuspensionReason    NVARCHAR(MAX)   NULL,
    SuspensionDate      DATE            NULL,
    CancellationReason  NVARCHAR(MAX)   NULL,
    CancellationDate    DATE            NULL,
    CreatedAt           DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    UpdatedAt           DATETIME2       NOT NULL        DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Licenses_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id),
    CONSTRAINT FK_Licenses_Applicants   FOREIGN KEY (ApplicantId)   REFERENCES Applicants(Id)
);
GO

-- ============================================================
-- NOTIFICATIONS
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
CREATE TABLE Notifications (
    Id              INT             IDENTITY(1,1)   PRIMARY KEY,
    RecipientType   NVARCHAR(20)    NOT NULL        CHECK(RecipientType IN ('APPLICANT','STAFF')),
    RecipientId     INT             NOT NULL,
    Channel         NVARCHAR(10)    NOT NULL        CHECK(Channel IN ('EMAIL','SMS','IN_APP')),
    Subject         NVARCHAR(500)   NULL,
    Message         NVARCHAR(MAX)   NOT NULL,
    Status          NVARCHAR(10)    NOT NULL        DEFAULT 'PENDING' CHECK(Status IN ('PENDING','SENT','FAILED')),
    SentAt          DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL        DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- IN-APP NOTIFICATIONS
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InAppNotifications')
CREATE TABLE InAppNotifications (
    Id              INT             IDENTITY(1,1)   PRIMARY KEY,
    RecipientType   NVARCHAR(20)    NOT NULL        CHECK(RecipientType IN ('APPLICANT','STAFF')),
    RecipientId     INT             NOT NULL,
    Title           NVARCHAR(500)   NOT NULL,
    Message         NVARCHAR(MAX)   NOT NULL,
    Link            NVARCHAR(1000)  NULL,
    IsRead          BIT             NOT NULL        DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL        DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- AUDIT LOGS (immutable)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
CREATE TABLE AuditLogs (
    Id          INT             IDENTITY(1,1)   PRIMARY KEY,
    ActorType   NVARCHAR(20)    NOT NULL        CHECK(ActorType IN ('APPLICANT','STAFF','SYSTEM')),
    ActorId     INT             NULL,
    ActorRole   NVARCHAR(50)    NULL,
    Action      NVARCHAR(200)   NOT NULL,
    EntityType  NVARCHAR(100)   NULL,
    EntityId    INT             NULL,
    OldValues   NVARCHAR(MAX)   NULL,   -- JSON
    NewValues   NVARCHAR(MAX)   NULL,   -- JSON
    IpAddress   NVARCHAR(50)    NULL,
    UserAgent   NVARCHAR(500)   NULL,
    CreatedAt   DATETIME2       NOT NULL        DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- SYSTEM CONFIG (key-value)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemConfig')
CREATE TABLE SystemConfig (
    [Key]       NVARCHAR(100)   NOT NULL        PRIMARY KEY,
    Value       NVARCHAR(MAX)   NOT NULL,
    Description NVARCHAR(500)   NULL,
    UpdatedBy   INT             NULL,
    UpdatedAt   DATETIME2       NOT NULL        DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- INDEXES
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Applications_ApplicantId')
    CREATE INDEX IX_Applications_ApplicantId   ON Applications(ApplicantId);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Applications_Status')
    CREATE INDEX IX_Applications_Status        ON Applications(Status);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Applications_District')
    CREATE INDEX IX_Applications_District      ON Applications(AddressDistrict);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Applications_RefNum')
    CREATE INDEX IX_Applications_RefNum        ON Applications(ReferenceNumber);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_Number')
    CREATE INDEX IX_Licenses_Number            ON Licenses(LicenseNumber);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_Status')
    CREATE INDEX IX_Licenses_Status            ON Licenses(Status);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_Expiry')
    CREATE INDEX IX_Licenses_Expiry            ON Licenses(ExpiryDate);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_Actor')
    CREATE INDEX IX_AuditLogs_Actor            ON AuditLogs(ActorId, ActorType);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_WorkflowActions_AppId')
    CREATE INDEX IX_WorkflowActions_AppId      ON WorkflowActions(ApplicationId);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InAppNotifications_Recipient')
    CREATE INDEX IX_InAppNotifications_Recipient ON InAppNotifications(RecipientId, RecipientType, IsRead);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UIX_Applicants_Email')
    CREATE UNIQUE INDEX UIX_Applicants_Email ON Applicants(Email) WHERE Email IS NOT NULL;
GO

PRINT 'PALMS v2 schema created successfully.';
GO
