-- Create CalendarShareInvites table
CREATE TABLE [dbo].[CalendarShareInvites] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [SenderUserId] INT NOT NULL,
    [RecipientEmail] NVARCHAR(255) NOT NULL,
    [RecipientUserId] INT NULL,
    [Permission] INT NOT NULL, -- 0 = Read, 1 = ReadWrite
    [Status] INT NOT NULL, -- 0 = Pending, 1 = Accepted, 2 = Rejected, 3 = Cancelled
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [RespondedAt] DATETIME2 NULL,
    CONSTRAINT [PK_CalendarShareInvites] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CalendarShareInvites_SenderUser] FOREIGN KEY ([SenderUserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CalendarShareInvites_RecipientUser] FOREIGN KEY ([RecipientUserId]) REFERENCES [dbo].[Users]([Id])
);

-- Create indexes for CalendarShareInvites
CREATE INDEX [IX_CalendarShareInvites_SenderUserId] ON [dbo].[CalendarShareInvites] ([SenderUserId]);
CREATE INDEX [IX_CalendarShareInvites_RecipientEmail] ON [dbo].[CalendarShareInvites] ([RecipientEmail]);
CREATE INDEX [IX_CalendarShareInvites_RecipientUserId] ON [dbo].[CalendarShareInvites] ([RecipientUserId]) WHERE [RecipientUserId] IS NOT NULL;
CREATE INDEX [IX_CalendarShareInvites_Status] ON [dbo].[CalendarShareInvites] ([Status]);

GO

-- Create CalendarShares table
CREATE TABLE [dbo].[CalendarShares] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [OwnerUserId] INT NOT NULL,
    [SharedWithUserId] INT NOT NULL,
    [Permission] INT NOT NULL, -- 0 = Read, 1 = ReadWrite
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_CalendarShares] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CalendarShares_OwnerUser] FOREIGN KEY ([OwnerUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT [FK_CalendarShares_SharedWithUser] FOREIGN KEY ([SharedWithUserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
    -- Prevent users from sharing their calendar with themselves
    CONSTRAINT [CK_CalendarShares_DifferentUsers] CHECK ([OwnerUserId] != [SharedWithUserId])
);

-- Create unique index to prevent duplicate shares
CREATE UNIQUE INDEX [IX_CalendarShares_OwnerUserId_SharedWithUserId] ON [dbo].[CalendarShares] ([OwnerUserId], [SharedWithUserId]);

-- Create indexes for efficient lookups
CREATE INDEX [IX_CalendarShares_OwnerUserId] ON [dbo].[CalendarShares] ([OwnerUserId]);
CREATE INDEX [IX_CalendarShares_SharedWithUserId] ON [dbo].[CalendarShares] ([SharedWithUserId]);

GO
