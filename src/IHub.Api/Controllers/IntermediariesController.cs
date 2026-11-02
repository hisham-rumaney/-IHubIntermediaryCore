using IHub.Application.DTOs;
using IHub.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace IHub.Api.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class IntermediariesController(IHubActivityService activityService) : ControllerBase
{
    [HttpGet("{intermediaryId}/statement")]
    [ProducesResponseType<StatementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStatementAsync(string intermediaryId, [FromQuery, BindRequired] DateTimeOffset start, [FromQuery, BindRequired] DateTimeOffset end, CancellationToken cancellationToken)
    {
        var result = await activityService.GetStatementAsync(intermediaryId, start, end, cancellationToken);
        return Ok(result);
    }
}
