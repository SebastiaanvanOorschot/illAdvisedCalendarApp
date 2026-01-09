-- Add recurring event fields to Events table
-- Run this manually in your SQL Server database

ALTER TABLE Events
ADD IsRecurring BIT NOT NULL DEFAULT 0,
    RecurrencePattern NVARCHAR(50) NULL,
    RecurrenceInterval INT NULL,
    RecurrenceEndDate DATETIME2 NULL,
    ParentEventId INT NULL;

-- Add foreign key constraint with ON DELETE NO ACTION
-- SQL Server doesn't allow ON DELETE SET NULL for self-referencing FKs
-- Deletion logic is handled in application code (WeekView, DayView)
ALTER TABLE Events
ADD CONSTRAINT FK_Events_ParentEvent
FOREIGN KEY (ParentEventId) REFERENCES Events(Id)
ON DELETE NO ACTION;

PRINT 'Recurring event fields added successfully!';
