using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Cycles;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/cycles/{cycleId}")]
public class CyclesController : ControllerBase
{
    private readonly ICycleService _service;

    public CyclesController(ICycleService service)
    {
        _service = service;
    }

    [HttpPatch]
    public async Task<ActionResult<CycleDto>> Update(long cycleId, CycleRequest request)
    {
        var outcome = await _service.UpdateAsync(cycleId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CycleResult.CycleNotFound =>
                NotFound(new ErrorResponse("指定されたサイクルが存在しません。")),
            CycleResult.Forbidden => this.ForbiddenError(),
            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(long cycleId)
    {
        var result = await _service.DeleteAsync(cycleId, this.GetCurrentUserId());

        return result switch
        {
            CycleResult.CycleNotFound =>
                NotFound(new ErrorResponse("指定されたサイクルが存在しません。")),
            CycleResult.Forbidden => this.ForbiddenError(),
            _ => NoContent(),
        };
    }
}
