-- Migration: Add UserId foreign key to Events table
-- Run this script in SSMS on your SebasDb database AFTER running 001_CreateUsersAndRefreshTokensTables.sql

-- Add UserId column to Events table
ALTER TABLE [dbo].[Events]
ADD [UserId] INT NULL;

-- Update existing events to belong to admin user (Id = 1)
-- Note: Adjust the UserId value if your admin user has a different Id
UPDATE [dbo].[Events]
SET [UserId] = 1
WHERE [UserId] IS NULL;

-- Make UserId NOT NULL after assigning existing events
ALTER TABLE [dbo].[Events]
ALTER COLUMN [UserId] INT NOT NULL;

-- Add foreign key constraint
ALTER TABLE [dbo].[Events]
ADD CONSTRAINT [FK_Events_Users] FOREIGN KEY ([UserId])
    REFERENCES [dbo].[Users]([Id])
    ON DELETE CASCADE;

-- Create index on UserId for faster event queries by user
CREATE NONCLUSTERED INDEX [IX_Events_UserId] ON [dbo].[Events] ([UserId]);

PRINT 'UserId column added to Events table successfully';
PRINT 'All existing events assigned to UserId = 1 (Admin User)';
