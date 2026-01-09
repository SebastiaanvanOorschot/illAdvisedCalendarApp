-- Migration: Add ShowEventTitleInMonthView preference to Users table
-- Date: 2026-01-09
-- Description: Adds a boolean field to track user preference for displaying event titles vs times in month view

ALTER TABLE Users
ADD ShowEventTitleInMonthView BIT NOT NULL DEFAULT 0;

-- Default value of 0 (false) means show time (current behavior)
-- Value of 1 (true) means show event title instead
