using IHub.Application.DTOs;
using IHub.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHub.Api.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class ActivitiesController(IHubActivityService activityService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ActivityResponse>> PostAsync([FromBody] CreateActivityRequest request, CancellationToken cancellationToken)
    {
        var (isCreated, response) = await activityService.SubmitActivityAsync(request, cancellationToken);
        return isCreated
            ? StatusCode(StatusCodes.Status201Created, response)
            : Ok(response);
    }
}
