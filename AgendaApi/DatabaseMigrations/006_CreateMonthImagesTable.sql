-- Create MonthImages table
CREATE TABLE [dbo].[MonthImages] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [Month] INT NOT NULL,
    [Year] INT NOT NULL,
    [FileName] NVARCHAR(500) NOT NULL,
    [ContentType] NVARCHAR(100) NOT NULL,
    [UploadedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_MonthImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MonthImages_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
);

-- Create unique index to ensure one image per user per month/year
CREATE UNIQUE INDEX [IX_MonthImages_UserId_Month_Year] ON [dbo].[MonthImages] ([UserId], [Month], [Year]);

-- Create index on UserId for faster lookups
CREATE INDEX [IX_MonthImages_UserId] ON [dbo].[MonthImages] ([UserId]);

GO
