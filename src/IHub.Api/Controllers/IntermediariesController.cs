using IHub.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHub.Api.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class IntermediariesController(IHubActivityService activityService) : ControllerBase
{
    [HttpGet("{intermediaryId}/statement")]
    public async Task<IActionResult> GetStatementAsync(string intermediaryId, [FromQuery] DateTimeOffset start, [FromQuery] DateTimeOffset end, CancellationToken cancellationToken)
    {
        var result = await activityService.GetStatementAsync(intermediaryId, start, end, cancellationToken);
        return Ok(result);
    }
}
