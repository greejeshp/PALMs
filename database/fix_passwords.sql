USE PalmsDb;
GO

-- Admin@1234
DECLARE @staffHash NVARCHAR(500) = N'$2a$11$AogBAHodTaYMkRpFyBSt..RYSYKK3bA6V2wOiBBcnJ6tQCIzGJSe.';
-- N@memail44
DECLARE @appHash NVARCHAR(500) = N'$2a$11$LM9QnOpDXzNq0mn0TFnOy.NQWTc.Z24gB7vVlpRRdR1.FzRAqnnuK';

PRINT 'Updating Staff Passwords...';
UPDATE Users SET 
    PasswordHash = @staffHash,
    FailedLoginCount = 0,
    IsLocked = 0
WHERE Username IN ('akc_dhanusha', 'akc_sarlahi', 'ppo_madhesh', 'accountant', 'chief');

PRINT 'Updating Applicant Password...';
UPDATE Applicants SET 
    PasswordHash = @appHash,
    IsVerified = 1,
    IsActive = 1
WHERE Mobile = '9801006102';

GO
SELECT Username, Role, LEFT(PasswordHash, 10) + '...' as Preview FROM Users WHERE Username IN ('akc_dhanusha', 'accountant', 'ppo_madhesh', 'chief');
SELECT Mobile, IsVerified, LEFT(PasswordHash, 10) + '...' as Preview FROM Applicants WHERE Mobile = '9801006102';
GO
