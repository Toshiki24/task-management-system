using TaskManagementSystem.Api.Dtos.Views;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class SavedViewServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SavedViewServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static SavedViewRequest Req(string name, bool shared, SavedViewFilters? filters = null) =>
        new(name, SavedViewType.List, shared, filters);

    [Fact(DisplayName = "M2 個人ビューは本人のみ、共有ビューは同一WSの所属者に見える")]
    public async Task GetByWorkspace_PersonalVsShared()
    {
        await using var ctx = _db.CreateContext();
        var a = await TestData.CreateUserAsync(ctx);
        var b = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, a.Id, WorkspaceMemberRole.Member);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, b.Id, WorkspaceMemberRole.Member);
        var service = new SavedViewService(ctx);

        await service.CreateAsync(ws.Id, Req("Aの個人", shared: false), a.Id);
        await service.CreateAsync(ws.Id, Req("Aの共有", shared: true), a.Id);

        // B には A の共有ビューだけ見える
        var forB = await service.GetByWorkspaceAsync(ws.Id, b.Id);
        Assert.NotNull(forB);
        Assert.Equal(new[] { "Aの共有" }, forB!.Select(v => v.Name));
        Assert.False(forB[0].IsOwner);

        // A には両方見える
        var forA = await service.GetByWorkspaceAsync(ws.Id, a.Id);
        Assert.Equal(2, forA!.Count);
        Assert.All(forA, v => Assert.True(v.IsOwner));
    }

    [Fact(DisplayName = "M2 非所属ユーザーには null(= 404)")]
    public async Task GetByWorkspace_NullForNonMember()
    {
        await using var ctx = _db.CreateContext();
        var outsider = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);

        Assert.Null(await new SavedViewService(ctx).GetByWorkspaceAsync(ws.Id, outsider.Id));
    }

    [Fact(DisplayName = "M2 既知の絞り込み条件が保存され読み出せる")]
    public async Task Create_RoundTripsFilters()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, user.Id, WorkspaceMemberRole.Member);

        var filters = new SavedViewFilters(
            new[] { "TODO", "IN_PROGRESS" }, "me", new[] { "HIGH" }, null, "ログイン", "-updatedAt");
        var created = await new SavedViewService(ctx).CreateAsync(ws.Id, Req("作業中", shared: false, filters), user.Id);

        Assert.Equal(CreateSavedViewResult.Success, created.Result);
        Assert.Equal(new[] { "TODO", "IN_PROGRESS" }, created.Data!.Filters.Status);
        Assert.Equal("me", created.Data.Filters.AssigneeId);
        Assert.Equal("ログイン", created.Data.Filters.Keyword);
        Assert.Equal("-updatedAt", created.Data.Filters.Sort);
    }

    [Fact(DisplayName = "M2 他人の個人ビューは編集・削除できない。WS Admin は可")]
    public async Task Update_Delete_Permissions()
    {
        await using var ctx = _db.CreateContext();
        var owner = await TestData.CreateUserAsync(ctx);
        var other = await TestData.CreateUserAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, owner.Id, WorkspaceMemberRole.Member);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, other.Id, WorkspaceMemberRole.Member);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new SavedViewService(ctx);

        var view = (await service.CreateAsync(ws.Id, Req("owner共有", shared: true), owner.Id)).Data!;

        // 他の一般メンバーは編集・削除できない
        Assert.Equal(UpdateSavedViewResult.Forbidden,
            (await service.UpdateAsync(ws.Id, view.Id, Req("書換", shared: true), other.Id)).Result);
        Assert.Equal(DeleteSavedViewResult.Forbidden,
            await service.DeleteAsync(ws.Id, view.Id, other.Id));

        // WS Admin は削除できる
        Assert.Equal(DeleteSavedViewResult.Success, await service.DeleteAsync(ws.Id, view.Id, admin.Id));
    }
}
