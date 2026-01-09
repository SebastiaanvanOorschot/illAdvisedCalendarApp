-- Migration: Add GoogleCalendarId field and performance index
-- Date: 2026-01-08
-- Description: Adds GoogleCalendarId to Events table to prevent duplicate imports
--              from shared Google Calendars. Includes composite index for performance.

BEGIN TRANSACTION;

-- Step 1: Add GoogleCalendarId column
ALTER TABLE Events
ADD GoogleCalendarId NVARCHAR(255) NULL;

-- Step 2: Create composite index for fast duplicate detection
-- This index makes lookups by (GoogleCalendarId, GoogleEventId) very fast
CREATE NONCLUSTERED INDEX IX_Events_GoogleCalendar_GoogleEvent
ON Events (GoogleCalendarId, GoogleEventId)
WHERE GoogleCalendarId IS NOT NULL AND GoogleEventId IS NOT NULL;

-- Step 3: Optionally update existing Google Calendar events
-- This sets GoogleCalendarId to 'unknown' for existing imported events
-- You can skip this if you want to re-import them with the correct calendar ID
UPDATE Events
SET GoogleCalendarId = 'migrated-unknown'
WHERE IsImportedFromGoogle = 1 AND GoogleCalendarId IS NULL;

COMMIT TRANSACTION;

-- Verification queries (run these to verify the migration)
-- SELECT COUNT(*) FROM Events WHERE GoogleCalendarId IS NOT NULL;
-- SELECT * FROM sys.indexes WHERE name = 'IX_Events_GoogleCalendar_GoogleEvent';
