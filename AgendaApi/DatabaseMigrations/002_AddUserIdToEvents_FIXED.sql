-- Migration: Add UserId foreign key to Events table
-- Run this script in SSMS on your SebasDb database AFTER running 001_CreateUsersAndRefreshTokensTables.sql

-- Check if the column already exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = 'UserId')
BEGIN
    -- Add UserId column to Events table as nullable first
    ALTER TABLE [dbo].[Events]
    ADD [UserId] INT NULL;

    PRINT 'UserId column added to Events table';
END
ELSE
BEGIN
    PRINT 'UserId column already exists';
END

-- Update existing events to belong to admin user (Id = 1)
UPDATE [dbo].[Events]
SET [UserId] = 1
WHERE [UserId] IS NULL;

PRINT 'Existing events assigned to UserId = 1 (Admin User)';

-- Make UserId NOT NULL after assigning existing events
ALTER TABLE [dbo].[Events]
ALTER COLUMN [UserId] INT NOT NULL;

PRINT 'UserId column set to NOT NULL';

-- Add foreign key constraint if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Events_Users]') AND parent_object_id = OBJECT_ID(N'[dbo].[Events]'))
BEGIN
    ALTER TABLE [dbo].[Events]
    ADD CONSTRAINT [FK_Events_Users] FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id])
        ON DELETE CASCADE;

    PRINT 'Foreign key constraint added';
END

-- Create index on UserId for faster event queries by user if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = 'IX_Events_UserId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Events_UserId] ON [dbo].[Events] ([UserId]);

    PRINT 'Index created on UserId';
END

PRINT 'Migration completed successfully!';
