-- Simplified migration script
-- Run this on SebasDb database

-- Step 1: Check how many events need updating
SELECT COUNT(*) AS EventsWithoutUserId FROM Events WHERE UserId IS NULL;

-- Step 2: Update all events without a UserId to admin user
UPDATE Events
SET UserId = 1
WHERE UserId IS NULL;

-- Step 3: Make UserId required (NOT NULL)
ALTER TABLE Events
ALTER COLUMN UserId INT NOT NULL;

-- Step 4: Add foreign key
ALTER TABLE Events
ADD CONSTRAINT FK_Events_Users FOREIGN KEY (UserId)
    REFERENCES Users(Id)
    ON DELETE CASCADE;

-- Step 5: Add index for performance
CREATE NONCLUSTERED INDEX IX_Events_UserId ON Events(UserId);

PRINT 'Migration completed!';
