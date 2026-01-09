-- Fix events that lost their recurrence information during editing
-- This restores RecurrenceRule and IsRecurring for imported Google Calendar events
-- that were accidentally converted to non-recurring

-- Find and display affected events first
SELECT Id, Title, GoogleEventId, IsImportedFromGoogle, IsLocallyModified, IsRecurring, RecurrenceRule
FROM Events
WHERE IsImportedFromGoogle = 1
  AND IsLocallyModified = 1
  AND (IsRecurring = 0 OR RecurrenceRule IS NULL OR RecurrenceRule = '')
  AND GoogleEventId IS NOT NULL;

-- To fix a specific event, you would need to know its original RecurrenceRule from Google Calendar
-- For now, this script just identifies the broken events
-- Manual fix would require re-importing or manually setting the RecurrenceRule

-- Example: If you know the event should be "FREQ=YEARLY" (annual holiday):
-- UPDATE Events
-- SET IsRecurring = 1, RecurrenceRule = 'FREQ=YEARLY'
-- WHERE GoogleEventId = 'specific-google-event-id';
