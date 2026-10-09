using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/repository-links/{linkId}")]
public class RepositoryLinksController : ControllerBase
{
    private readonly IRepositoryLinkService _service;

    public RepositoryLinksController(IRepositoryLinkService service)
    {
        _service = service;
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(long linkId)
    {
        var result = await _service.DeleteAsync(linkId, this.GetCurrentUserId());

        return result switch
        {
            RepositoryLinkResult.LinkNotFound =>
                NotFound(new ErrorResponse("指定された連携が存在しません。")),
            RepositoryLinkResult.Forbidden => this.ForbiddenError(),
            _ => NoContent(),
        };
    }
}
