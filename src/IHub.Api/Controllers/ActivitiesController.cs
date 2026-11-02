using IHub.Application.DTOs;
using IHub.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHub.Api.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class ActivitiesController(IHubActivityService activityService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ActivityResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ActivityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivityResponse>> PostAsync([FromBody] CreateActivityRequest request, CancellationToken cancellationToken)
    {
        var (isCreated, response) = await activityService.SubmitActivityAsync(request, cancellationToken);
        return isCreated
            ? StatusCode(StatusCodes.Status201Created, response)
            : Ok(response);
    }
}
