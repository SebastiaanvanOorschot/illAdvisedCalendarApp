-- Migration: Add Calendar Subscriptions feature
-- Date: 2026-01-09
-- Description: Adds support for iCal calendar subscriptions (e.g., Parro school agenda)

-- Create CalendarSubscriptions table
CREATE TABLE CalendarSubscriptions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    ICalUrl NVARCHAR(2000) NOT NULL,
    Color NVARCHAR(7) NULL, -- Hex color code like #FF69B4
    SyncIntervalMinutes INT NOT NULL DEFAULT 60,
    IsActive BIT NOT NULL DEFAULT 1,
    LastSyncedAt DATETIME2 NULL,
    LastSyncError NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL,
    UserId INT NOT NULL,

    CONSTRAINT FK_CalendarSubscriptions_Users FOREIGN KEY (UserId)
        REFERENCES Users(Id) ON DELETE CASCADE
);

-- Create indexes for CalendarSubscriptions
CREATE INDEX IX_CalendarSubscriptions_UserId ON CalendarSubscriptions(UserId);
CREATE INDEX IX_CalendarSubscriptions_UserId_IsActive ON CalendarSubscriptions(UserId, IsActive);

-- Add columns to Events table for iCal subscription support
ALTER TABLE Events ADD CalendarSubscriptionId INT NULL;
ALTER TABLE Events ADD ExternalEventId NVARCHAR(500) NULL;
ALTER TABLE Events ADD IsFromSubscription BIT NOT NULL DEFAULT 0;
ALTER TABLE Events ADD IsReadOnly BIT NOT NULL DEFAULT 0;

-- Create foreign key for CalendarSubscriptionId
ALTER TABLE Events ADD CONSTRAINT FK_Events_CalendarSubscriptions
    FOREIGN KEY (CalendarSubscriptionId) REFERENCES CalendarSubscriptions(Id) ON DELETE CASCADE;

-- Create indexes for subscription events
CREATE INDEX IX_Events_CalendarSubscriptionId ON Events(CalendarSubscriptionId)
    WHERE CalendarSubscriptionId IS NOT NULL;
CREATE INDEX IX_Events_ExternalEventId ON Events(ExternalEventId)
    WHERE ExternalEventId IS NOT NULL;
CREATE INDEX IX_Events_IsFromSubscription ON Events(IsFromSubscription)
    WHERE IsFromSubscription = 1;

GO

PRINT 'Migration 009_AddCalendarSubscriptions completed successfully';
