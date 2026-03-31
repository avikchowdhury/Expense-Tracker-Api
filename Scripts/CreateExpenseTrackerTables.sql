-- Database creation (if needed)
 USE master;
 IF DB_ID('ExpenseTrackerDb') IS NULL
 BEGIN
     CREATE DATABASE ExpenseTrackerDb;
 END
 GO

USE ExpenseTrackerDb;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Users table
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL
    DROP TABLE dbo.Users;
GO

CREATE TABLE dbo.Users
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    Role NVARCHAR(64) NOT NULL DEFAULT('User')
);
GO

CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email);
GO

-- Receipts table
IF OBJECT_ID('dbo.Receipts', 'U') IS NOT NULL
    DROP TABLE dbo.Receipts;
GO

CREATE TABLE dbo.Receipts
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId INT NOT NULL,
    UploadedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FileName NVARCHAR(512) NOT NULL,
    BlobUrl NVARCHAR(MAX) NULL,
    TotalAmount DECIMAL(18,2) NOT NULL DEFAULT(0),
    Vendor NVARCHAR(256) NULL,
    Category NVARCHAR(256) NULL,
    ParsedContentJson NVARCHAR(MAX) NULL,
    CONSTRAINT FK_Receipts_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
GO

-- Expenses table
IF OBJECT_ID('dbo.Expenses', 'U') IS NOT NULL
    DROP TABLE dbo.Expenses;
GO

CREATE TABLE dbo.Expenses
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId INT NOT NULL,
    ReceiptId INT NULL,
    Date DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Amount DECIMAL(18,2) NOT NULL,
    Category NVARCHAR(256) NOT NULL DEFAULT('Uncategorized'),
    Description NVARCHAR(MAX) NULL,
    Currency NVARCHAR(16) NOT NULL DEFAULT('USD'),
    CONSTRAINT FK_Expenses_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Expenses_Receipts FOREIGN KEY (ReceiptId)
        REFERENCES dbo.Receipts(Id) ON DELETE NO ACTION
);
GO

-- Budgets table
IF OBJECT_ID('dbo.Budgets', 'U') IS NOT NULL
    DROP TABLE dbo.Budgets;
GO

CREATE TABLE dbo.Budgets
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId INT NOT NULL,
    Category NVARCHAR(256) NOT NULL DEFAULT('General'),
    MonthlyLimit DECIMAL(18,2) NOT NULL,
    CurrentSpent DECIMAL(18,2) NOT NULL DEFAULT(0),
    LastReset DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Budgets_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
GO

PRINT 'ExpenseTracker schema created successfully.';
