-- Migration: Add Exception Date Support for Recurring Events
-- Description: Adds ExceptionDates and RecurrenceId fields to support EXDATE (RFC 5545)
-- Date: 2025-12-17

-- Add ExceptionDates column to store comma-separated exception dates
ALTER TABLE [dbo].[Events] ADD [ExceptionDates] NVARCHAR(MAX) NULL;

-- Add RecurrenceId column to link modified occurrences to their original date
ALTER TABLE [dbo].[Events] ADD [RecurrenceId] DATETIME2 NULL;

-- Add index on RecurrenceId for faster lookups of modified occurrences
CREATE INDEX [IX_Events_RecurrenceId] ON [dbo].[Events]([RecurrenceId]);

-- Add index on ParentEventId + RecurrenceId for finding modified occurrences of a series
CREATE INDEX [IX_Events_ParentEventId_RecurrenceId] ON [dbo].[Events]([ParentEventId], [RecurrenceId])
WHERE [RecurrenceId] IS NOT NULL;
