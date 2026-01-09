using AgendaApi.Data;
using AgendaApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Services;

public class CalendarShareService
{
    private readonly AgendaDbContext _context;

    public CalendarShareService(AgendaDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Send a calendar share invite to another user by email
    /// </summary>
    public async Task<CalendarShareInvite> SendInviteAsync(int senderUserId, string recipientEmail, SharePermission permission)
    {
        // Normalize email
        recipientEmail = recipientEmail.Trim().ToLower();

        // Check if sender exists
        var sender = await _context.Users.FindAsync(senderUserId);
        if (sender == null)
            throw new ArgumentException("Sender user not found");

        // Prevent sending invite to self
        if (sender.Email.ToLower() == recipientEmail)
            throw new InvalidOperationException("Cannot share calendar with yourself");

        // Check if recipient user exists
        var recipientUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == recipientEmail);

        // Check if there's already an active share
        if (recipientUser != null)
        {
            var existingShare = await _context.CalendarShares
                .FirstOrDefaultAsync(cs => cs.OwnerUserId == senderUserId && cs.SharedWithUserId == recipientUser.Id);

            if (existingShare != null)
                throw new InvalidOperationException("Calendar is already shared with this user");
        }

        // Check if there's already a pending invite
        var existingInvite = await _context.CalendarShareInvites
            .FirstOrDefaultAsync(csi =>
                csi.SenderUserId == senderUserId &&
                csi.RecipientEmail == recipientEmail &&
                csi.Status == InviteStatus.Pending);

        if (existingInvite != null)
            throw new InvalidOperationException("A pending invite already exists for this email");

        // Create the invite
        var invite = new CalendarShareInvite
        {
            SenderUserId = senderUserId,
            RecipientEmail = recipientEmail,
            RecipientUserId = recipientUser?.Id,
            Permission = permission,
            Status = InviteStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.CalendarShareInvites.Add(invite);
        await _context.SaveChangesAsync();

        // Load sender info for response
        await _context.Entry(invite).Reference(i => i.SenderUser).LoadAsync();

        return invite;
    }

    /// <summary>
    /// Get all pending invites received by a user
    /// </summary>
    public async Task<List<CalendarShareInvite>> GetReceivedInvitesAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

        // Fetch all pending invites and filter in memory for case-insensitive comparison
        var allPendingInvites = await _context.CalendarShareInvites
            .Include(csi => csi.SenderUser)
            .Where(csi => csi.Status == InviteStatus.Pending)
            .ToListAsync();

        return allPendingInvites
            .Where(csi => csi.RecipientEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(csi => csi.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Get all pending invites sent by a user
    /// </summary>
    public async Task<List<CalendarShareInvite>> GetSentInvitesAsync(int userId)
    {
        return await _context.CalendarShareInvites
            .Include(csi => csi.RecipientUser)
            .Where(csi => csi.SenderUserId == userId && csi.Status == InviteStatus.Pending)
            .OrderByDescending(csi => csi.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Accept a calendar share invite
    /// </summary>
    public async Task<CalendarShare> AcceptInviteAsync(int inviteId, int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

        var invite = await _context.CalendarShareInvites
            .Include(csi => csi.SenderUser)
            .FirstOrDefaultAsync(csi => csi.Id == inviteId);

        if (invite == null)
            throw new ArgumentException("Invite not found");

        // Verify the invite is for this user
        if (invite.RecipientEmail.ToLower() != user.Email.ToLower())
            throw new UnauthorizedAccessException("This invite is not for you");

        if (invite.Status != InviteStatus.Pending)
            throw new InvalidOperationException("This invite has already been responded to");

        // Update invite status
        invite.Status = InviteStatus.Accepted;
        invite.RespondedAt = DateTime.UtcNow;
        invite.RecipientUserId = userId;

        // Create the calendar share
        var share = new CalendarShare
        {
            OwnerUserId = invite.SenderUserId,
            SharedWithUserId = userId,
            Permission = invite.Permission,
            CreatedAt = DateTime.UtcNow
        };

        _context.CalendarShares.Add(share);
        await _context.SaveChangesAsync();

        // Load relationships for response
        await _context.Entry(share).Reference(s => s.OwnerUser).LoadAsync();
        await _context.Entry(share).Reference(s => s.SharedWithUser).LoadAsync();

        return share;
    }

    /// <summary>
    /// Reject a calendar share invite
    /// </summary>
    public async Task RejectInviteAsync(int inviteId, int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

        var invite = await _context.CalendarShareInvites.FindAsync(inviteId);
        if (invite == null)
            throw new ArgumentException("Invite not found");

        // Verify the invite is for this user
        if (invite.RecipientEmail.ToLower() != user.Email.ToLower())
            throw new UnauthorizedAccessException("This invite is not for you");

        if (invite.Status != InviteStatus.Pending)
            throw new InvalidOperationException("This invite has already been responded to");

        invite.Status = InviteStatus.Rejected;
        invite.RespondedAt = DateTime.UtcNow;
        invite.RecipientUserId = userId;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Cancel a sent invite (by the sender)
    /// </summary>
    public async Task CancelInviteAsync(int inviteId, int userId)
    {
        var invite = await _context.CalendarShareInvites.FindAsync(inviteId);
        if (invite == null)
            throw new ArgumentException("Invite not found");

        if (invite.SenderUserId != userId)
            throw new UnauthorizedAccessException("You can only cancel your own invites");

        if (invite.Status != InviteStatus.Pending)
            throw new InvalidOperationException("Only pending invites can be cancelled");

        invite.Status = InviteStatus.Cancelled;
        invite.RespondedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get all active calendar shares (calendars shared with the user)
    /// </summary>
    public async Task<List<CalendarShare>> GetSharedWithMeAsync(int userId)
    {
        return await _context.CalendarShares
            .Include(cs => cs.OwnerUser)
            .Where(cs => cs.SharedWithUserId == userId)
            .OrderByDescending(cs => cs.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get all active calendar shares (calendars I'm sharing with others)
    /// </summary>
    public async Task<List<CalendarShare>> GetMySharesAsync(int userId)
    {
        return await _context.CalendarShares
            .Include(cs => cs.SharedWithUser)
            .Where(cs => cs.OwnerUserId == userId)
            .OrderByDescending(cs => cs.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Remove a calendar share
    /// </summary>
    public async Task RemoveShareAsync(int shareId, int userId)
    {
        var share = await _context.CalendarShares.FindAsync(shareId);
        if (share == null)
            throw new ArgumentException("Share not found");

        // Only the owner or the person being shared with can remove the share
        if (share.OwnerUserId != userId && share.SharedWithUserId != userId)
            throw new UnauthorizedAccessException("You don't have permission to remove this share");

        _context.CalendarShares.Remove(share);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Update the permission level of an existing share
    /// </summary>
    public async Task<CalendarShare> UpdateSharePermissionAsync(int shareId, int userId, SharePermission newPermission)
    {
        var share = await _context.CalendarShares.FindAsync(shareId);
        if (share == null)
            throw new ArgumentException("Share not found");

        // Only the owner can update permissions
        if (share.OwnerUserId != userId)
            throw new UnauthorizedAccessException("Only the calendar owner can update permissions");

        share.Permission = newPermission;
        share.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Load relationships for response
        await _context.Entry(share).Reference(s => s.OwnerUser).LoadAsync();
        await _context.Entry(share).Reference(s => s.SharedWithUser).LoadAsync();

        return share;
    }

    /// <summary>
    /// Check if a user has permission to access an event
    /// </summary>
    public async Task<(bool HasAccess, SharePermission? Permission)> CheckEventAccessAsync(int userId, int eventOwnerId)
    {
        // Owner always has full access
        if (userId == eventOwnerId)
            return (true, SharePermission.ReadWrite);

        // Check if there's a share relationship
        var share = await _context.CalendarShares
            .FirstOrDefaultAsync(cs => cs.OwnerUserId == eventOwnerId && cs.SharedWithUserId == userId);

        if (share != null)
            return (true, share.Permission);

        return (false, null);
    }

    /// <summary>
    /// Get all user IDs whose calendars are shared with the current user
    /// </summary>
    public async Task<List<int>> GetSharedCalendarOwnerIdsAsync(int userId)
    {
        return await _context.CalendarShares
            .Where(cs => cs.SharedWithUserId == userId)
            .Select(cs => cs.OwnerUserId)
            .ToListAsync();
    }
}
