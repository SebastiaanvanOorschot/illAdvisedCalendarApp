-- Final migration script with proper schema
-- Run this on SebasDb database

-- Step 1: Check how many events need updating
SELECT COUNT(*) AS EventsWithoutUserId FROM [dbo].[Events] WHERE [UserId] IS NULL;

-- Step 2: Update all events without a UserId to admin user
UPDATE [dbo].[Events]
SET [UserId] = 1
WHERE [UserId] IS NULL;

-- Step 3: Make UserId required (NOT NULL)
ALTER TABLE [dbo].[Events]
ALTER COLUMN [UserId] INT NOT NULL;

-- Step 4: Add foreign key (check if it exists first)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Events_Users')
BEGIN
    ALTER TABLE [dbo].[Events]
    ADD CONSTRAINT [FK_Events_Users] FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id])
        ON DELETE CASCADE;
END

-- Step 5: Add index for performance (check if it exists first)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Events_UserId' AND object_id = OBJECT_ID('[dbo].[Events]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Events_UserId] ON [dbo].[Events]([UserId]);
END

PRINT 'Migration completed!';
