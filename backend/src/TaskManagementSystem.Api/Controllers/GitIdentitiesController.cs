using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/git-identities/{identityId}")]
public class GitIdentitiesController : ControllerBase
{
    private readonly IGitIdentityService _service;

    public GitIdentitiesController(IGitIdentityService service)
    {
        _service = service;
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(long identityId)
    {
        var result = await _service.DeleteAsync(identityId, this.GetCurrentUserId());

        return result switch
        {
            GitIdentityResult.IdentityNotFound =>
                NotFound(new ErrorResponse("指定された対応付けが存在しません。")),
            GitIdentityResult.Forbidden => this.ForbiddenError(),
            _ => NoContent(),
        };
    }
}
