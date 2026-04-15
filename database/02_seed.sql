-- ============================================================
-- PALMS v2 — Seed Data (Demo / Development)
-- Run AFTER 01_schema.sql
-- Password for all staff: Admin@1234
-- BCrypt hash of 'Admin@1234' (cost=12)
-- ============================================================

USE PalmsDb;
GO

-- ============================================================
-- AKCs
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Akcs)
BEGIN
    INSERT INTO Akcs (Name, District, Areas, IsActive) VALUES
        (N'AKC Dhanusha',   N'Dhanusha',    N'["Janakpur","Dhanushadham","Sabaila"]',        1),
        (N'AKC Sarlahi',    N'Sarlahi',     N'["Malangwa","Haripur","Kaudena"]',             1),
        (N'AKC Mahottari',  N'Mahottari',   N'["Jaleshwar","Bardibas","Gaushala"]',          1),
        (N'AKC Siraha',     N'Siraha',      N'["Siraha","Golbazaar","Lahan"]',               1),
        (N'AKC Rautahat',   N'Rautahat',    N'["Gaur","Chandrapur","Rajpur"]',               1),
        (N'AKC Bara',       N'Bara',        N'["Kalaiya","Nijgadh","Simraungadh"]',          1),
        (N'AKC Parsa',      N'Parsa',       N'["Birgunj","Pokhariya","Bahuarwa"]',           1),
        (N'AKC Saptari',    N'Saptari',     N'["Rajbiraj","Kanchanrup","Rupani"]',           1);
END
GO

-- ============================================================
-- STAFF USERS
-- BCrypt hash for: Admin@1234  (cost 12)
-- ============================================================
DECLARE @hash NVARCHAR(500) = N'$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMqJqhcanFp8RRnWAdiOL31Ure';

IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (Username, Email, PasswordHash, FullName, Role, District, IsActive) VALUES
        ('admin',       'admin@palms.gov.np',       @hash, N'System Administrator',      'ADMIN',        NULL,           1),
        ('akc_dhanusha','akc.dhanusha@palms.gov.np', @hash, N'Ram Prasad Yadav',          'AKC_OFFICIAL', N'Dhanusha',    1),
        ('akc_sarlahi', 'akc.sarlahi@palms.gov.np',  @hash, N'Sita Kumari Sah',           'AKC_OFFICIAL', N'Sarlahi',     1),
        ('ppo_madhesh', 'ppo@palms.gov.np',          @hash, N'Mohan Lal Jha',             'PPO',          NULL,           1),
        ('accountant',  'accounts@palms.gov.np',     @hash, N'Rekha Devi Thakur',         'ACCOUNTANT',   NULL,           1),
        ('chief',       'chief@palms.gov.np',        @hash, N'Dr. Ramesh Kumar Singh',    'CHIEF',        NULL,           1);
END
GO

-- Update AKC foreign keys
UPDATE Users SET AkcId = (SELECT Id FROM Akcs WHERE District = N'Dhanusha') WHERE Username = 'akc_dhanusha';
UPDATE Users SET AkcId = (SELECT Id FROM Akcs WHERE District = N'Sarlahi')  WHERE Username = 'akc_sarlahi';
GO

-- ============================================================
-- DEMO APPLICANT
-- Password: Applicant@1234
-- BCrypt hash
-- ============================================================
DECLARE @appHash NVARCHAR(500) = N'$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWToit2';

IF NOT EXISTS (SELECT 1 FROM Applicants WHERE Mobile = '9841000001')
BEGIN
    INSERT INTO Applicants (Mobile, Email, PasswordHash, FullName, IsVerified, IsActive)
    VALUES ('9841000001', 'demo@applicant.com', @appHash, N'Krishna Bahadur Mahato', 1, 1);

    DECLARE @applicantId INT = SCOPE_IDENTITY();

    INSERT INTO ApplicantProfiles
        (ApplicantId, FirmName, RegistrationNumber, PanVatNumber, AuthorizedPersonName,
         CitizenshipNumber, AddressGapaNapa, AddressWard, AddressDistrict,
         Phone, EducationalQualification, ProfileComplete)
    VALUES
        (@applicantId, N'Janaki Krishi Sewa Kendra', N'REG-2080-001', N'123456789',
         N'Krishna Bahadur Mahato', N'27-01-77-02345',
         N'Janakpur Sub-Metropolitan', N'4', N'Dhanusha',
         '9841000001', N'B.Sc. Agriculture', 1);
END
GO

-- ============================================================
-- SYSTEM CONFIG (default key-value settings)
-- ============================================================
INSERT INTO SystemConfig ([Key], Value, Description) 
SELECT * FROM (VALUES
    ('renewal_fee',             '2500',   'Standard renewal fee in NPR'),
    ('late_fee',                '5000',   'Late renewal penalty fee in NPR'),
    ('late_deadline_days',      '35',     'Days after expiry before late fee kicks in'),
    ('otp_expiry_minutes',      '10',     'OTP validity in minutes'),
    ('otp_max_attempts',        '3',      'Max OTP attempts before lockout'),
    ('session_timeout_minutes', '30',     'Session inactivity timeout'),
    ('max_failed_logins',       '5',      'Account lock after N failed logins'),
    ('reminder_days_1',         '60',     'First renewal reminder days before expiry'),
    ('reminder_days_2',         '30',     'Second renewal reminder days before expiry'),
    ('reminder_days_3',         '7',      'Third renewal reminder days before expiry'),
    ('sla_akc_hours',           '48',     'SLA hours for AKC review'),
    ('sla_ppo_hours',           '72',     'SLA hours for PPO review'),
    ('max_upload_mb',           '5',      'Max file upload size in MB'),
    ('app_name',                'PALMS',  'Application name'),
    ('app_org',                 'Directorate of Agriculture Development, Madhesh Province', 'Organisation name')
) AS v([Key], Value, Description)
WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE [Key] = v.[Key]);
GO

PRINT 'PALMS v2 seed data inserted successfully.';
GO
