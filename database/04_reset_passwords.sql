USE PalmsDb;
GO

-- Reset all staff passwords to: Admin@1234
DECLARE @staffHash NVARCHAR(500) = N'$2a$11$AogBAHodTaYMkRpFyBSt..RYSYKK3bA6V2wOiBBcnJ6tQCIzGJSe.';

UPDATE Users SET 
    PasswordHash = @staffHash,
    FailedLoginCount = 0,
    IsLocked = 0
WHERE Username IN ('admin', 'akc_dhanusha', 'akc_sarlahi', 'ppo_madhesh', 'accountant', 'chief');

PRINT 'Staff passwords reset to Admin@1234. Rows affected: ' + CAST(@@ROWCOUNT AS VARCHAR);
GO

-- Reset applicant password to: Applicant@1234
DECLARE @appHash NVARCHAR(500) = N'$2a$11$71BduX7emciKWHJYKYkgrusxeugGVhf7QhTpRCCqaJjk8pe4tR0lS';

UPDATE Applicants SET 
    PasswordHash = @appHash
WHERE Mobile = '9841000001';

PRINT 'Applicant password reset to Applicant@1234. Rows affected: ' + CAST(@@ROWCOUNT AS VARCHAR);
GO

-- Verify passwords are set
SELECT Id, Username, Role, IsActive, IsLocked, FailedLoginCount,
       LEFT(PasswordHash, 20) + '...' AS HashPreview
FROM Users
ORDER BY Id;
GO
