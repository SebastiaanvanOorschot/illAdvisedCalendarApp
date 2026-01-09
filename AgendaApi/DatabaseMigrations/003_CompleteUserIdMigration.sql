-- Migration: Complete UserId setup (run after column is created)
-- This script assumes UserId column already exists from running the ALTER TABLE statement

-- Step 1: Update existing events to belong to admin user (Id = 1)
UPDATE [dbo].[Events]
SET [UserId] = 1
WHERE [UserId] IS NULL;

PRINT 'Step 1: Existing events assigned to UserId = 1 (Admin User)';

-- Step 2: Make UserId NOT NULL
ALTER TABLE [dbo].[Events]
ALTER COLUMN [UserId] INT NOT NULL;

PRINT 'Step 2: UserId column set to NOT NULL';

-- Step 3: Add foreign key constraint
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Events_Users]'))
BEGIN
    ALTER TABLE [dbo].[Events]
    ADD CONSTRAINT [FK_Events_Users] FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id])
        ON DELETE CASCADE;

    PRINT 'Step 3: Foreign key constraint added';
END
ELSE
BEGIN
    PRINT 'Step 3: Foreign key constraint already exists';
END

-- Step 4: Create index on UserId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = 'IX_Events_UserId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Events_UserId] ON [dbo].[Events] ([UserId]);
    PRINT 'Step 4: Index created on UserId';
END
ELSE
BEGIN
    PRINT 'Step 4: Index already exists';
END

PRINT 'Migration completed successfully!';
