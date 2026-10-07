using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class DependencyServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public DependencyServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static AddDependencyRequest BlockedBy(long taskId) =>
        new(taskId, DependencyRelations.BlockedBy);

    private static AddDependencyRequest Blocks(long taskId) =>
        new(taskId, DependencyRelations.Blocks);

    [Fact(DisplayName = "M2 BLOCKED_BY/BLOCKS を追加すると両タスクから対称に見える")]
    public async Task Add_BlockedByAndBlocks_VisibleFromBothSides()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);
        var b = await TestData.CreateTaskAsync(ctx, project.Id);

        // A は B に待たされる(A blockedBy B) → 辺 B→A
        var added = await service.AddAsync(a.Id, BlockedBy(b.Id), owner.Id);
        Assert.Equal(AddDependencyResult.Success, added.Result);
        Assert.Equal(b.Id, added.Data!.TaskId);

        var aDeps = await service.GetByTaskAsync(a.Id, owner.Id);
        Assert.Equal(new[] { b.Id }, aDeps!.BlockedBy.Select(d => d.TaskId));
        Assert.Empty(aDeps.Blocking);

        // 相手 B からは blocking として見える
        var bDeps = await service.GetByTaskAsync(b.Id, owner.Id);
        Assert.Equal(new[] { a.Id }, bDeps!.Blocking.Select(d => d.TaskId));
        Assert.Empty(bDeps.BlockedBy);
    }

    [Fact(DisplayName = "M2 自己依存は InvalidTarget")]
    public async Task Add_SelfReference_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);

        Assert.Equal(AddDependencyResult.InvalidTarget,
            (await service.AddAsync(a.Id, BlockedBy(a.Id), owner.Id)).Result);
    }

    [Fact(DisplayName = "M2 別プロジェクトのタスクは InvalidTarget")]
    public async Task Add_CrossProject_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);
        var otherProject = await TestData.CreateProjectAsync(ctx);
        var foreign = await TestData.CreateTaskAsync(ctx, otherProject.Id);

        Assert.Equal(AddDependencyResult.InvalidTarget,
            (await service.AddAsync(a.Id, BlockedBy(foreign.Id), owner.Id)).Result);
    }

    [Fact(DisplayName = "M2 同じ依存辺の二重登録は Duplicate")]
    public async Task Add_Duplicate_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);
        var b = await TestData.CreateTaskAsync(ctx, project.Id);

        Assert.Equal(AddDependencyResult.Success, (await service.AddAsync(a.Id, BlockedBy(b.Id), owner.Id)).Result);
        // 同じ辺(B→A)を逆向きの指定でもう一度 = B BLOCKS A
        Assert.Equal(AddDependencyResult.Duplicate, (await service.AddAsync(b.Id, Blocks(a.Id), owner.Id)).Result);
    }

    [Fact(DisplayName = "M2 循環する依存は Cycle(直接・間接)")]
    public async Task Add_Cycle_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);
        var b = await TestData.CreateTaskAsync(ctx, project.Id);
        var c = await TestData.CreateTaskAsync(ctx, project.Id);

        // A→B, B→C(A が B を、B が C をブロック)
        Assert.Equal(AddDependencyResult.Success, (await service.AddAsync(a.Id, Blocks(b.Id), owner.Id)).Result);
        Assert.Equal(AddDependencyResult.Success, (await service.AddAsync(b.Id, Blocks(c.Id), owner.Id)).Result);

        // 直接の逆(B→A)は循環
        Assert.Equal(AddDependencyResult.Cycle, (await service.AddAsync(b.Id, Blocks(a.Id), owner.Id)).Result);
        // 間接の逆(C→A)も循環
        Assert.Equal(AddDependencyResult.Cycle, (await service.AddAsync(c.Id, Blocks(a.Id), owner.Id)).Result);
    }

    [Fact(DisplayName = "M2 完了カテゴリ(DONE)のブロッカーは IsClosed=true")]
    public async Task Get_ClosedBlocker_IsClosedTrue()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);
        var blocker = await TestData.CreateTaskAsync(ctx, project.Id);
        blocker.Status = TaskItemStatus.Done;
        await ctx.SaveChangesAsync();

        await service.AddAsync(a.Id, BlockedBy(blocker.Id), owner.Id);

        var deps = await service.GetByTaskAsync(a.Id, owner.Id);
        Assert.True(deps!.BlockedBy.Single().IsClosed);
    }

    [Fact(DisplayName = "M2 Viewer は依存を追加できない(403 相当)")]
    public async Task Add_Viewer_Forbidden()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var viewer = await TestData.CreateUserAsync(ctx);
        var workspaceId = await ctx.Projects.Where(p => p.Id == project.Id).Select(p => p.WorkspaceId).SingleAsync();
        await TestData.AddWorkspaceMemberAsync(ctx, workspaceId, viewer.Id, WorkspaceMemberRole.Viewer);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);
        var b = await TestData.CreateTaskAsync(ctx, project.Id);

        Assert.Equal(AddDependencyResult.Forbidden, (await service.AddAsync(a.Id, BlockedBy(b.Id), viewer.Id)).Result);
    }

    [Fact(DisplayName = "M2 非所属ユーザーには依存一覧を開示しない(null=404 相当)")]
    public async Task Get_NonMember_ReturnsNull()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);

        Assert.Null(await service.GetByTaskAsync(a.Id, outsider.Id));
    }

    [Fact(DisplayName = "M2 依存を削除できる。無関係な ID は NotFound")]
    public async Task Delete_RemovesEdge_UnrelatedIsNotFound()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new DependencyService(ctx);
        var a = await TestData.CreateTaskAsync(ctx, project.Id);
        var b = await TestData.CreateTaskAsync(ctx, project.Id);
        var c = await TestData.CreateTaskAsync(ctx, project.Id);

        var edge = (await service.AddAsync(a.Id, BlockedBy(b.Id), owner.Id)).Data!;

        // C は辺に関与していないので削除できない
        Assert.Equal(DeleteDependencyResult.NotFound, await service.DeleteAsync(c.Id, edge.DependencyId, owner.Id));
        // 関与する A からは削除できる
        Assert.Equal(DeleteDependencyResult.Success, await service.DeleteAsync(a.Id, edge.DependencyId, owner.Id));
        Assert.Empty((await service.GetByTaskAsync(a.Id, owner.Id))!.BlockedBy);
    }

    [Fact(DisplayName = "M2 タスク削除で依存辺も連動削除される(DB の ON DELETE CASCADE)")]
    public async Task DeleteTask_CascadesDependencies()
    {
        long blockerId;
        long blockedId;
        await using (var ctx = _db.CreateContext())
        {
            var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
            var service = new DependencyService(ctx);
            var a = await TestData.CreateTaskAsync(ctx, project.Id);
            var b = await TestData.CreateTaskAsync(ctx, project.Id);
            await service.AddAsync(a.Id, BlockedBy(b.Id), owner.Id);
            blockerId = b.Id;
            blockedId = a.Id;
        }

        // 追跡の後始末に左右されないよう、別コンテキストで削除して DB 側のカスケードを検証する
        await using (var ctx = _db.CreateContext())
        {
            ctx.Tasks.Remove(await ctx.Tasks.FindAsync(blockerId) ?? throw new InvalidOperationException());
            await ctx.SaveChangesAsync();
        }

        // クラス内でDBを共有するため、この辺だけに絞って消えたことを確認する
        await using (var ctx = _db.CreateContext())
        {
            Assert.False(await ctx.TaskDependencies
                .AnyAsync(d => d.BlockingTaskId == blockerId || d.BlockedTaskId == blockedId));
        }
    }
}
