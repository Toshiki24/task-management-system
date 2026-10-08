using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Milestones;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/milestones/{milestoneId}")]
public class MilestonesController : ControllerBase
{
    private readonly IMilestoneService _service;

    public MilestonesController(IMilestoneService service)
    {
        _service = service;
    }

    [HttpPatch]
    public async Task<ActionResult<MilestoneDto>> Update(long milestoneId, MilestoneRequest request)
    {
        var outcome = await _service.UpdateAsync(milestoneId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            MilestoneResult.MilestoneNotFound =>
                NotFound(new ErrorResponse("指定されたマイルストーンが存在しません。")),
            MilestoneResult.Forbidden => this.ForbiddenError(),
            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(long milestoneId)
    {
        var result = await _service.DeleteAsync(milestoneId, this.GetCurrentUserId());

        return result switch
        {
            MilestoneResult.MilestoneNotFound =>
                NotFound(new ErrorResponse("指定されたマイルストーンが存在しません。")),
            MilestoneResult.Forbidden => this.ForbiddenError(),
            _ => NoContent(),
        };
    }
}
