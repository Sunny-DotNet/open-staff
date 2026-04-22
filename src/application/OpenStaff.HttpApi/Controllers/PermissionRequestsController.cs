using Microsoft.AspNetCore.Mvc;
using OpenStaff.Agent.Services;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/permission-requests")]
public class PermissionRequestsController : ControllerBase
{
    private readonly IPermissionRequestHandler _permissionRequestHandler;

    public PermissionRequestsController(IPermissionRequestHandler permissionRequestHandler)
    {
        _permissionRequestHandler = permissionRequestHandler;
    }

    [HttpPost("listeners")]
    public ActionResult<PermissionListenerLease> RegisterListener([FromBody] PermissionListenerRegistrationBody? body)
    {
        var lease = _permissionRequestHandler.RegisterClientListener(body?.ListenerId);
        return Ok(lease);
    }

    [HttpDelete("listeners/{listenerId}")]
    public ActionResult<ApiStatusDto> UnregisterListener(string listenerId)
    {
        _permissionRequestHandler.UnregisterClientListener(listenerId);
        return Ok(new ApiStatusDto { Status = "unregistered" });
    }

    [HttpPost("{requestId}/responses")]
    public async Task<ActionResult<PermissionAuthorizationSubmitResult>> SubmitResponse(
        string requestId,
        [FromBody] PermissionRequestResponseBody body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.ListenerId))
            return BadRequest(new ApiMessageDto { Message = "listenerId 不能为空" });

        PermissionAuthorizationKind kind;
        switch (body.Kind?.Trim().ToLowerInvariant())
        {
            case "accept":
                kind = PermissionAuthorizationKind.Accept;
                break;
            case "reject":
                kind = PermissionAuthorizationKind.Reject;
                break;
            default:
                return BadRequest(new ApiMessageDto { Message = "kind 必须是 accept 或 reject" });
        }

        var result = await _permissionRequestHandler.SubmitAsync(
            new PermissionAuthorizationResponse(requestId, kind, body.ListenerId),
            ct);
        return Ok(result);
    }
}

public sealed class PermissionListenerRegistrationBody
{
    public string? ListenerId { get; set; }
}

public sealed class PermissionRequestResponseBody
{
    public string ListenerId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
}
