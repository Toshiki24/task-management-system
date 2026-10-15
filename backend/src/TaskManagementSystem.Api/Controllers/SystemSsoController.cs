using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Sso;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// SSO(OIDC)接続設定の管理(System Admin 専用。Phase 2 M5 §7)。
/// クライアントシークレットは参照のみを保持し、値そのものは返さない。
/// </summary>
[ApiController]
[Route("api/system/sso-connections")]
public class SystemSsoController : ControllerBase
{
    private readonly ISsoService _ssoService;

    public SystemSsoController(ISsoService ssoService)
    {
        _ssoService = ssoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<SsoConnectionDto>>> GetAll()
    {
        var connections = await _ssoService.GetConnectionsAsync(this.GetCurrentUserId());
        if (connections is null)
        {
            return this.ForbiddenError();
        }

        return Ok(connections);
    }

    [HttpPost]
    public async Task<ActionResult<SsoConnectionDto>> Create(CreateSsoConnectionRequest request)
    {
        var outcome = await _ssoService.CreateConnectionAsync(this.GetCurrentUserId(), request);
        return Map(outcome) ?? StatusCode(StatusCodes.Status201Created, outcome.Data);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SsoConnectionDto>> Update(long id, UpdateSsoConnectionRequest request)
    {
        var outcome = await _ssoService.UpdateConnectionAsync(this.GetCurrentUserId(), id, request);
        return Map(outcome) ?? Ok(outcome.Data);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _ssoService.DeleteConnectionAsync(this.GetCurrentUserId(), id);
        return result switch
        {
            DeleteSsoConnectionResult.Forbidden => this.ForbiddenError(),
            DeleteSsoConnectionResult.NotFound =>
                NotFound(new ErrorResponse("指定された SSO 接続が存在しません。")),
            _ => NoContent(),
        };
    }

    /// <summary>エラー結果なら対応する ActionResult を、成功なら null を返す。</summary>
    private ActionResult? Map(SsoConnectionOutcome outcome) => outcome.Result switch
    {
        SsoConnectionResult.Forbidden => this.ForbiddenError(),
        SsoConnectionResult.NotFound =>
            NotFound(new ErrorResponse("指定された SSO 接続が存在しません。")),
        SsoConnectionResult.WorkspaceNotFound =>
            NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),
        _ => null,
    };
}
