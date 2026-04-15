-- ============================================================
-- PALMS v2 — Migration: Multiple AKC Assignment
-- Run this AFTER 01_schema.sql
-- ============================================================

USE PalmsDb;
GO

-- Create junction table for User <-> AKC many-to-many
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserAkcs')
BEGIN
    CREATE TABLE UserAkcs (
        Id      INT IDENTITY(1,1) PRIMARY KEY,
        UserId  INT NOT NULL,
        AkcId   INT NOT NULL,
        CONSTRAINT FK_UserAkcs_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserAkcs_Akc  FOREIGN KEY (AkcId)  REFERENCES Akcs(Id)  ON DELETE CASCADE,
        CONSTRAINT UQ_UserAkcs UNIQUE (UserId, AkcId)
    );

    -- Migrate existing single AkcId assignments
    INSERT INTO UserAkcs (UserId, AkcId)
    SELECT Id, AkcId FROM Users
    WHERE AkcId IS NOT NULL AND Role = 'AKC_OFFICIAL';

    PRINT 'UserAkcs table created and migrated existing AKC assignments.';
END
GO
