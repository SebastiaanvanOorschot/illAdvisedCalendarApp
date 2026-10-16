using AgendaApi.Data;
using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Tests.Services;

public sealed class CalendarShareServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AgendaDbContext _context;
    private readonly CalendarShareService _service;
    private readonly User _alice;
    private readonly User _bob;
    private readonly User _carol;

    public CalendarShareServiceTests()
    {
        _alice = _database.AddUser("Alice");
        _bob = _database.AddUser("Bob");
        _carol = _database.AddUser("Carol");
        _context = _database.CreateContext();
        _service = new CalendarShareService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    // ── sending invites ─────────────────────────────────────────────────────

    [Fact]
    public async Task SendInvite_ToRegisteredUser_StoresNormalisedEmailAndLinksRecipient()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "  BOB@Example.TEST ", SharePermission.Read);

        Assert.Equal("bob@example.test", invite.RecipientEmail);
        Assert.Equal(_bob.Id, invite.RecipientUserId);
        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Equal(SharePermission.Read, invite.Permission);
        Assert.Equal("Alice", invite.SenderUser?.Name);
    }

    [Fact]
    public async Task SendInvite_ToUnknownEmail_IsAllowedWithoutRecipientUser()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "new.person@example.test", SharePermission.ReadWrite);

        Assert.Null(invite.RecipientUserId);
    }

    [Fact]
    public async Task SendInvite_ToYourself_IsRejectedCaseInsensitively()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SendInviteAsync(_alice.Id, "ALICE@example.test", SharePermission.Read));
    }

    [Fact]
    public async Task SendInvite_WhenAlreadyShared_IsRejected()
    {
        _database.AddShare(_alice, _bob, SharePermission.Read);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read));
    }

    [Fact]
    public async Task SendInvite_WhenAPendingInviteExists_IsRejected()
    {
        await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SendInviteAsync(_alice.Id, "Bob@Example.test", SharePermission.ReadWrite));
    }

    [Fact]
    public async Task SendInvite_FromUnknownSender_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SendInviteAsync(9999, "bob@example.test", SharePermission.Read));
    }

    // ── listing invites ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetReceivedInvites_MatchesEmailCaseInsensitively_AndOnlyPending()
    {
        var fromAlice = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);
        var fromCarol = await _service.SendInviteAsync(_carol.Id, "bob@example.test", SharePermission.Read);
        await _service.CancelInviteAsync(fromCarol.Id, _carol.Id);

        var received = await _service.GetReceivedInvitesAsync(_bob.Id);

        Assert.Equal(new[] { fromAlice.Id }, received.Select(i => i.Id));
    }

    [Fact]
    public async Task GetSentInvites_ReturnsOnlyTheSendersPendingInvites()
    {
        var toBob = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);
        await _service.SendInviteAsync(_carol.Id, "bob@example.test", SharePermission.Read);

        var sent = await _service.GetSentInvitesAsync(_alice.Id);

        Assert.Equal(new[] { toBob.Id }, sent.Select(i => i.Id));
    }

    // ── responding to invites ───────────────────────────────────────────────

    [Fact]
    public async Task AcceptInvite_CreatesShareWithTheInvitedPermission()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.ReadWrite);

        var share = await _service.AcceptInviteAsync(invite.Id, _bob.Id);

        Assert.Equal(_alice.Id, share.OwnerUserId);
        Assert.Equal(_bob.Id, share.SharedWithUserId);
        Assert.Equal(SharePermission.ReadWrite, share.Permission);
        var stored = await FreshInvite(invite.Id);
        Assert.Equal(InviteStatus.Accepted, stored.Status);
        Assert.NotNull(stored.RespondedAt);
    }

    [Fact]
    public async Task AcceptInvite_ForSomeoneElse_IsRejected()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.AcceptInviteAsync(invite.Id, _carol.Id));
        Assert.Empty(await Shares());
    }

    [Fact]
    public async Task AcceptInvite_Twice_IsRejected()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);
        await _service.AcceptInviteAsync(invite.Id, _bob.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AcceptInviteAsync(invite.Id, _bob.Id));
    }

    [Fact]
    public async Task AcceptInvite_ThatWasCancelled_IsRejected()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);
        await _service.CancelInviteAsync(invite.Id, _alice.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AcceptInviteAsync(invite.Id, _bob.Id));
        Assert.Empty(await Shares());
    }

    [Fact]
    public async Task RejectInvite_MarksItRejectedWithoutCreatingAShare()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);

        await _service.RejectInviteAsync(invite.Id, _bob.Id);

        Assert.Equal(InviteStatus.Rejected, (await FreshInvite(invite.Id)).Status);
        Assert.Empty(await Shares());
    }

    [Fact]
    public async Task RejectInvite_ForSomeoneElse_IsRejected()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RejectInviteAsync(invite.Id, _carol.Id));
    }

    [Fact]
    public async Task CancelInvite_ByTheRecipient_IsRejected()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CancelInviteAsync(invite.Id, _bob.Id));
    }

    [Fact]
    public async Task CancelInvite_AfterItWasAccepted_IsRejected()
    {
        var invite = await _service.SendInviteAsync(_alice.Id, "bob@example.test", SharePermission.Read);
        await _service.AcceptInviteAsync(invite.Id, _bob.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelInviteAsync(invite.Id, _alice.Id));
    }

    // ── managing shares ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("owner")]
    [InlineData("recipient")]
    public async Task RemoveShare_ByOwnerOrRecipient_Removes(string who)
    {
        var share = _database.AddShare(_alice, _bob, SharePermission.Read);

        await _service.RemoveShareAsync(share.Id, who == "owner" ? _alice.Id : _bob.Id);

        Assert.Empty(await Shares());
    }

    [Fact]
    public async Task RemoveShare_ByThirdParty_IsRejected()
    {
        var share = _database.AddShare(_alice, _bob, SharePermission.Read);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RemoveShareAsync(share.Id, _carol.Id));
        Assert.Single(await Shares());
    }

    [Fact]
    public async Task UpdateSharePermission_ByOwner_ChangesIt()
    {
        var share = _database.AddShare(_alice, _bob, SharePermission.Read);

        var updated = await _service.UpdateSharePermissionAsync(share.Id, _alice.Id, SharePermission.ReadWrite);

        Assert.Equal(SharePermission.ReadWrite, updated.Permission);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateSharePermission_ByRecipient_IsRejected()
    {
        // the recipient must not be able to upgrade their own access
        var share = _database.AddShare(_alice, _bob, SharePermission.Read);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.UpdateSharePermissionAsync(share.Id, _bob.Id, SharePermission.ReadWrite));
        Assert.Equal(SharePermission.Read, (await Shares()).Single().Permission);
    }

    // ── access checks ───────────────────────────────────────────────────────

    [Fact]
    public async Task CheckEventAccess_Owner_HasReadWrite()
    {
        Assert.Equal((true, SharePermission.ReadWrite), await _service.CheckEventAccessAsync(_alice.Id, _alice.Id));
    }

    [Theory]
    [InlineData(SharePermission.Read)]
    [InlineData(SharePermission.ReadWrite)]
    public async Task CheckEventAccess_SharedWithYou_HasTheSharedPermission(SharePermission permission)
    {
        _database.AddShare(_alice, _bob, permission);

        Assert.Equal((true, permission), await _service.CheckEventAccessAsync(_bob.Id, _alice.Id));
    }

    [Fact]
    public async Task CheckEventAccess_OnlyWorksInTheDirectionOfTheShare()
    {
        _database.AddShare(_alice, _bob, SharePermission.ReadWrite);

        Assert.Equal((false, (SharePermission?)null), await _service.CheckEventAccessAsync(_alice.Id, _bob.Id));
        Assert.Equal((false, (SharePermission?)null), await _service.CheckEventAccessAsync(_carol.Id, _alice.Id));
    }

    [Fact]
    public async Task GetSharedCalendarOwnerIds_ListsOwnersWhoSharedWithYou()
    {
        _database.AddShare(_alice, _bob, SharePermission.Read);
        _database.AddShare(_carol, _bob, SharePermission.ReadWrite);
        _database.AddShare(_bob, _alice, SharePermission.Read);

        var owners = await _service.GetSharedCalendarOwnerIdsAsync(_bob.Id);

        Assert.Equal(new[] { _alice.Id, _carol.Id }.Order(), owners.Order());
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<CalendarShareInvite> FreshInvite(int id)
    {
        using var context = _database.CreateContext();
        return await context.CalendarShareInvites.SingleAsync(i => i.Id == id);
    }

    private async Task<List<CalendarShare>> Shares()
    {
        using var context = _database.CreateContext();
        return await context.CalendarShares.ToListAsync();
    }
}
