using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class TransitionRuleServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public TransitionRuleServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static PutTransitionRulesRequest Rules(params TransitionRuleInput[] rules) => new(rules.ToList());

    [Fact(DisplayName = "M4 WS 既定ルールの置き換えは WS Admin のみ、一般メンバーは不可")]
    public async Task ReplaceWorkspace_RequiresAdmin()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, member.Id, WorkspaceMemberRole.Member);
        var service = new TransitionRuleService(ctx);
        var payload = Rules(new TransitionRuleInput(TransitionTrigger.PrMerged, "DONE", true));

        Assert.Equal(TransitionRuleResult.Forbidden, (await service.ReplaceWorkspaceAsync(workspace.Id, payload, member.Id)).Result);
        var ok = await service.ReplaceWorkspaceAsync(workspace.Id, payload, admin.Id);
        Assert.Equal(TransitionRuleResult.Success, ok.Result);
        Assert.Single(ok.Data!);
    }

    [Fact(DisplayName = "M4 遷移先が WS のワークフローに無いキーなら InvalidStatus")]
    public async Task ReplaceWorkspace_InvalidStatus()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new TransitionRuleService(ctx);

        var result = await service.ReplaceWorkspaceAsync(
            workspace.Id, Rules(new TransitionRuleInput(TransitionTrigger.PrMerged, "NOPE", true)), admin.Id);
        Assert.Equal(TransitionRuleResult.InvalidStatus, result.Result);
    }

    [Fact(DisplayName = "M4 解決: プロジェクト個別ルールが WS 既定を上書きする")]
    public async Task Resolve_ProjectOverridesWorkspace()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TransitionRuleService(ctx);

        await service.ReplaceWorkspaceAsync(project.WorkspaceId,
            Rules(new TransitionRuleInput(TransitionTrigger.PrMerged, "DONE", true)), owner.Id);

        // WS 既定のみ → DONE
        Assert.Equal("DONE", await service.ResolveToStatusAsync(project.Id, project.WorkspaceId, TransitionTrigger.PrMerged));

        // プロジェクト個別で IN_PROGRESS に上書き
        await service.ReplaceProjectAsync(project.Id,
            Rules(new TransitionRuleInput(TransitionTrigger.PrMerged, "IN_PROGRESS", true)), owner.Id);
        Assert.Equal("IN_PROGRESS", await service.ResolveToStatusAsync(project.Id, project.WorkspaceId, TransitionTrigger.PrMerged));
    }

    [Fact(DisplayName = "M4 解決: 無効化(enabled=false)ルールは解決しない")]
    public async Task Resolve_DisabledIgnored()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TransitionRuleService(ctx);
        await service.ReplaceWorkspaceAsync(project.WorkspaceId,
            Rules(new TransitionRuleInput(TransitionTrigger.PrMerged, "DONE", false)), owner.Id);

        Assert.Null(await service.ResolveToStatusAsync(project.Id, project.WorkspaceId, TransitionTrigger.PrMerged));
    }

    [Fact(DisplayName = "M4 プロジェクトルールの置き換えは OWNER/WS Admin のみ")]
    public async Task ReplaceProject_RequiresManage()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var service = new TransitionRuleService(ctx);
        var payload = Rules(new TransitionRuleInput(TransitionTrigger.BranchCreated, "IN_PROGRESS", true));

        Assert.Equal(TransitionRuleResult.Forbidden, (await service.ReplaceProjectAsync(project.Id, payload, member.Id)).Result);
        Assert.Equal(TransitionRuleResult.Success, (await service.ReplaceProjectAsync(project.Id, payload, owner.Id)).Result);
    }

    [Fact(DisplayName = "M4 非所属ユーザーには一覧を開示しない(null)")]
    public async Task List_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new TransitionRuleService(ctx).GetByProjectAsync(project.Id, outsider.Id));
    }
}
