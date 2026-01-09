-- Migration: Create Users and RefreshTokens tables
-- Run this script in SSMS on your SebasDb database

-- Create Users table
CREATE TABLE [dbo].[Users] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [GoogleId] NVARCHAR(255) NOT NULL,
    [Email] NVARCHAR(255) NOT NULL,
    [Name] NVARCHAR(255) NOT NULL,
    [ProfilePictureUrl] NVARCHAR(500) NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [LastLoginAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Users_GoogleId] UNIQUE NONCLUSTERED ([GoogleId] ASC),
    CONSTRAINT [UQ_Users_Email] UNIQUE NONCLUSTERED ([Email] ASC)
);

-- Create RefreshTokens table
CREATE TABLE [dbo].[RefreshTokens] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [Token] NVARCHAR(500) NOT NULL,
    [ExpiresAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [RevokedAt] DATETIME2 NULL,
    [IsRevoked] BIT NOT NULL DEFAULT 0,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_RefreshTokens_Token] UNIQUE NONCLUSTERED ([Token] ASC)
);

-- Create index on UserId for faster lookups
CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId] ON [dbo].[RefreshTokens] ([UserId]);

-- Create admin user for existing events
INSERT INTO [dbo].[Users] ([GoogleId], [Email], [Name], [ProfilePictureUrl], [CreatedAt], [LastLoginAt])
VALUES
    ('admin-temp-user', 'admin@localhost', 'Admin User (Temporary)', NULL, GETUTCDATE(), GETUTCDATE());

PRINT 'Users and RefreshTokens tables created successfully';
PRINT 'Admin user created with Id: ' + CAST(SCOPE_IDENTITY() AS NVARCHAR(10));
