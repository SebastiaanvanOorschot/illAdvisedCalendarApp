-- Migration: Add indexes to optimize event occurrence queries
-- Date: 2025-12-18
-- Description: Adds indexes to improve performance when filtering events by date range and recurrence

-- Index for all events filtered by user and date range
-- This covers both recurring and non-recurring events
CREATE NONCLUSTERED INDEX IX_Events_UserId_StartDateTime_Recurrence
ON Events (UserId, StartDateTime, IsRecurring)
INCLUDE (Id, Title, Description, EndDateTime, Color, RecurrenceRule, RecurrenceEndDate, ExceptionDates, GoogleEventId, IsImportedFromGoogle, IsLocallyModified, RecurrenceId, ParentEventId);

-- Index specifically for recurring events
CREATE NONCLUSTERED INDEX IX_Events_UserId_Recurring
ON Events (UserId, IsRecurring, StartDateTime, RecurrenceEndDate)
WHERE IsRecurring = 1;

GO
