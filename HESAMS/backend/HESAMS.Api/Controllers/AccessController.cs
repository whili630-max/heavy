using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HESAMS.Api.Controllers;

public record ScanRequest(string ReaderId, string CardUid);
public record EndSessionRequest(string ReaderId, decimal? HoursAdded, decimal? FuelConsumed);

/// <summary>
/// Called by ESP32 gate units. Authenticated with a per-device token
/// (X-Device-Token header) validated by DeviceTokenMiddleware in production;
/// [AllowAnonymous] here so devices don't need user JWTs.
/// </summary>
[ApiController]
[Route("api/access")]
public class AccessController : ControllerBase
{
    private readonly AccessValidationService _engine;
    public AccessController(AccessValidationService engine) { _engine = engine; }

    /// <summary>Run the 9-gate interlock. Returns unlock=true only if all gates pass.</summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> Validate(ScanRequest req, CancellationToken ct)
    {
        var result = await _engine.ValidateAsync(req.ReaderId, req.CardUid, ct);
        return Ok(new
        {
            unlock = result.Granted,
            reason = result.DenialReason,
            eventId = result.EventId,
            trace = result.Trace
        });
    }

    /// <summary>Machine switched off / operator tapped out.</summary>
    [HttpPost("end-session")]
    [AllowAnonymous]
    public async Task<IActionResult> EndSession(EndSessionRequest req, CancellationToken ct)
    {
        await _engine.EndSessionAsync(req.ReaderId, req.HoursAdded, req.FuelConsumed, ct);
        return NoContent();
    }
}
