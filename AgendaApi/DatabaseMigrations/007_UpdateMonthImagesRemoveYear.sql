-- Drop existing unique index that includes Year
DROP INDEX [IX_MonthImages_UserId_Month_Year] ON [dbo].[MonthImages];

-- Drop Year column
ALTER TABLE [dbo].[MonthImages] DROP COLUMN [Year];

-- Create new unique index on UserId and Month only
CREATE UNIQUE INDEX [IX_MonthImages_UserId_Month] ON [dbo].[MonthImages] ([UserId], [Month]);

GO
