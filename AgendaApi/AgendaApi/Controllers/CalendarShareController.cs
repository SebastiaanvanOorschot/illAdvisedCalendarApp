using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarShareController : ControllerBase
{
    private readonly CalendarShareService _shareService;

    public CalendarShareController(CalendarShareService shareService)
    {
        _shareService = shareService;
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim!);
    }

    /// <summary>
    /// Send a calendar share invite to another user by email
    /// </summary>
    [HttpPost("invites")]
    public async Task<ActionResult<CalendarShareInvite>> SendInvite([FromBody] SendInviteRequest request)
    {
        try
        {
            var userId = GetUserId();
            var invite = await _shareService.SendInviteAsync(userId, request.RecipientEmail, request.Permission);
            return Ok(invite);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get all pending invites received by the current user
    /// </summary>
    [HttpGet("invites/received")]
    public async Task<ActionResult<List<CalendarShareInvite>>> GetReceivedInvites()
    {
        try
        {
            var userId = GetUserId();
            var invites = await _shareService.GetReceivedInvitesAsync(userId);
            return Ok(invites);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get all pending invites sent by the current user
    /// </summary>
    [HttpGet("invites/sent")]
    public async Task<ActionResult<List<CalendarShareInvite>>> GetSentInvites()
    {
        var userId = GetUserId();
        var invites = await _shareService.GetSentInvitesAsync(userId);
        return Ok(invites);
    }

    /// <summary>
    /// Accept a calendar share invite
    /// </summary>
    [HttpPost("invites/{inviteId}/accept")]
    public async Task<ActionResult<CalendarShare>> AcceptInvite(int inviteId)
    {
        try
        {
            var userId = GetUserId();
            var share = await _shareService.AcceptInviteAsync(inviteId, userId);
            return Ok(share);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reject a calendar share invite
    /// </summary>
    [HttpPost("invites/{inviteId}/reject")]
    public async Task<ActionResult> RejectInvite(int inviteId)
    {
        try
        {
            var userId = GetUserId();
            await _shareService.RejectInviteAsync(inviteId, userId);
            return Ok(new { message = "Invite rejected successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a sent invite (by the sender)
    /// </summary>
    [HttpDelete("invites/{inviteId}")]
    public async Task<ActionResult> CancelInvite(int inviteId)
    {
        try
        {
            var userId = GetUserId();
            await _shareService.CancelInviteAsync(inviteId, userId);
            return Ok(new { message = "Invite cancelled successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get all calendars shared with me
    /// </summary>
    [HttpGet("shared-with-me")]
    public async Task<ActionResult<List<CalendarShare>>> GetSharedWithMe()
    {
        var userId = GetUserId();
        var shares = await _shareService.GetSharedWithMeAsync(userId);
        return Ok(shares);
    }

    /// <summary>
    /// Get all my shares (calendars I'm sharing with others)
    /// </summary>
    [HttpGet("my-shares")]
    public async Task<ActionResult<List<CalendarShare>>> GetMyShares()
    {
        var userId = GetUserId();
        var shares = await _shareService.GetMySharesAsync(userId);
        return Ok(shares);
    }

    /// <summary>
    /// Remove a calendar share
    /// </summary>
    [HttpDelete("shares/{shareId}")]
    public async Task<ActionResult> RemoveShare(int shareId)
    {
        try
        {
            var userId = GetUserId();
            await _shareService.RemoveShareAsync(shareId, userId);
            return Ok(new { message = "Share removed successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update the permission level of an existing share
    /// </summary>
    [HttpPut("shares/{shareId}/permission")]
    public async Task<ActionResult<CalendarShare>> UpdateSharePermission(int shareId, [FromBody] UpdatePermissionRequest request)
    {
        try
        {
            var userId = GetUserId();
            var share = await _shareService.UpdateSharePermissionAsync(shareId, userId, request.Permission);
            return Ok(share);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

// DTOs
public record SendInviteRequest(string RecipientEmail, SharePermission Permission);
public record UpdatePermissionRequest(SharePermission Permission);
