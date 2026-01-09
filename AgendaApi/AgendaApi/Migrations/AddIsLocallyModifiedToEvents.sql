-- Migration: Add IsLocallyModified column to Events table
-- Date: 2025-12-18
-- Description: Adds IsLocallyModified flag to track when imported Google Calendar events
--              have been modified locally, preventing sync overwrites

-- Add the new column with default value of 0 (false)
ALTER TABLE Events
ADD IsLocallyModified BIT NOT NULL DEFAULT 0;

-- Optional: Add helpful comment
EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Indicates if an imported event was modified locally (prevents sync overwrites)',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'Events',
    @level2type = N'COLUMN', @level2name = 'IsLocallyModified';

GO
