using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class SystemAdminServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SystemAdminServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private async Task<User> CreateSystemAdminAsync(AppDbContext ctx)
    {
        var user = await TestData.CreateUserAsync(ctx);
        user.IsSystemAdmin = true;
        await ctx.SaveChangesAsync();
        return user;
    }

    [Fact(DisplayName = "一覧・付与・剥奪は System Admin 以外は Forbidden/null")]
    public async Task Operations_ForbiddenForNonAdmin()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var target = await TestData.CreateUserAsync(ctx);
        var service = new SystemAdminService(ctx);

        Assert.Null(await service.GetAdminsAsync(user.Id));
        Assert.Null(await service.GetAuditLogsAsync(user.Id, new AuditLogQuery()));
        Assert.Null(await service.GetStatsAsync(user.Id));
        Assert.Null(await service.GetWorkspacesAsync(user.Id));
        Assert.Null(await service.GetUsersAsync(user.Id));
        Assert.Equal(GrantSystemAdminResult.Forbidden, await service.GrantAsync(target.Id, user.Id));
        Assert.Equal(RevokeSystemAdminResult.Forbidden, await service.RevokeAsync(target.Id, user.Id));
    }

    [Fact(DisplayName = "System Admin の付与と監査記録")]
    public async Task GrantAsync_SetsFlagAndRecordsAudit()
    {
        await using var ctx = _db.CreateContext();
        var admin = await CreateSystemAdminAsync(ctx);
        var target = await TestData.CreateUserAsync(ctx);
        var service = new SystemAdminService(ctx);

        Assert.Equal(GrantSystemAdminResult.Success, await service.GrantAsync(target.Id, admin.Id));

        await using var assert = _db.CreateContext();
        Assert.True((await assert.Users.SingleAsync(u => u.Id == target.Id)).IsSystemAdmin);
        Assert.True(await assert.AuditLogs.AnyAsync(a =>
            a.Action == AuditActions.SystemAdminGranted && a.TargetId == target.Id && a.ActorUserId == admin.Id));
    }

    [Fact(DisplayName = "既に System Admin への付与は AlreadyAdmin")]
    public async Task GrantAsync_AlreadyAdmin()
    {
        await using var ctx = _db.CreateContext();
        var admin = await CreateSystemAdminAsync(ctx);
        var service = new SystemAdminService(ctx);

        Assert.Equal(GrantSystemAdminResult.AlreadyAdmin, await service.GrantAsync(admin.Id, admin.Id));
        Assert.Equal(GrantSystemAdminResult.UserNotFound,
            await service.GrantAsync(TestData.NonExistentId, admin.Id));
    }

    [Fact(DisplayName = "最後の System Admin は剥奪できない")]
    public async Task RevokeAsync_LastAdminGuard()
    {
        await using var ctx = _db.CreateContext();
        // 「最後の1人」判定はインスタンス全体の件数で決まるため、他テストが残した System Admin を一旦リセットする
        await ctx.Users.Where(u => u.IsSystemAdmin)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsSystemAdmin, false));
        var admin = await CreateSystemAdminAsync(ctx);
        var service = new SystemAdminService(ctx);

        // 唯一の System Admin を剥奪 → ブロック
        Assert.Equal(RevokeSystemAdminResult.LastAdmin, await service.RevokeAsync(admin.Id, admin.Id));

        // 2人目を付与してからなら剥奪できる
        var second = await TestData.CreateUserAsync(ctx);
        await service.GrantAsync(second.Id, admin.Id);
        Assert.Equal(RevokeSystemAdminResult.Success, await service.RevokeAsync(second.Id, admin.Id));

        await using var assert = _db.CreateContext();
        Assert.False((await assert.Users.SingleAsync(u => u.Id == second.Id)).IsSystemAdmin);
    }

    [Fact(DisplayName = "System Admin でないユーザーの剥奪は NotAdmin")]
    public async Task RevokeAsync_NotAdmin()
    {
        await using var ctx = _db.CreateContext();
        var admin = await CreateSystemAdminAsync(ctx);
        var plain = await TestData.CreateUserAsync(ctx);
        var service = new SystemAdminService(ctx);

        Assert.Equal(RevokeSystemAdminResult.NotAdmin, await service.RevokeAsync(plain.Id, admin.Id));
    }

    [Fact(DisplayName = "重要操作が監査ログに記録され、System Admin が参照できる")]
    public async Task GetAuditLogsAsync_ReturnsRecordedOperations()
    {
        await using var ctx = _db.CreateContext();
        var admin = await CreateSystemAdminAsync(ctx);

        // System Admin によるワークスペース作成で監査ログが1件記録される
        var created = await new WorkspaceService(ctx).CreateAsync(new WorkspaceRequest("監査用WS", null), admin.Id);
        Assert.Equal(CreateWorkspaceResult.Success, created.Result);

        var logs = await new SystemAdminService(ctx).GetAuditLogsAsync(admin.Id, new AuditLogQuery());

        Assert.NotNull(logs);
        Assert.True(logs!.Total >= 1);
        Assert.Contains(logs.Items, a =>
            a.Action == AuditActions.WorkspaceCreated && a.ActorUserId == admin.Id && a.ActorName == admin.Name);
    }

    [Fact(DisplayName = "監査ログはアクション・アクターでフィルタでき、総件数も返す")]
    public async Task GetAuditLogsAsync_FiltersAndPaging()
    {
        await using var ctx = _db.CreateContext();
        var admin = await CreateSystemAdminAsync(ctx);
        var service = new SystemAdminService(ctx);

        // 付与→剥奪で user.system_admin.granted / revoked を記録する(2人目を用意)
        var target = await TestData.CreateUserAsync(ctx);
        await service.GrantAsync(target.Id, admin.Id);
        await service.RevokeAsync(target.Id, admin.Id);

        var granted = await service.GetAuditLogsAsync(
            admin.Id, new AuditLogQuery(Action: AuditActions.SystemAdminGranted));
        Assert.NotNull(granted);
        Assert.All(granted!.Items, a => Assert.Equal(AuditActions.SystemAdminGranted, a.Action));
        Assert.Contains(granted.Items, a => a.TargetId == target.Id);

        // アクター絞り込み
        var byActor = await service.GetAuditLogsAsync(admin.Id, new AuditLogQuery(ActorUserId: admin.Id));
        Assert.NotNull(byActor);
        Assert.All(byActor!.Items, a => Assert.Equal(admin.Id, a.ActorUserId));

        // limit=1 でも総件数は全件を表す
        var firstPage = await service.GetAuditLogsAsync(
            admin.Id, new AuditLogQuery(ActorUserId: admin.Id, Limit: 1));
        Assert.NotNull(firstPage);
        Assert.Single(firstPage!.Items);
        Assert.True(firstPage.Total >= 2);
    }

    [Fact(DisplayName = "統計・全WS・全ユーザー一覧を System Admin が取得できる")]
    public async Task GetStatsWorkspacesUsers_ReturnsData()
    {
        await using var ctx = _db.CreateContext();
        var admin = await CreateSystemAdminAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        var service = new SystemAdminService(ctx);

        var created = await new WorkspaceService(ctx).CreateAsync(new WorkspaceRequest("統計用WS", null), admin.Id);
        Assert.Equal(CreateWorkspaceResult.Success, created.Result);

        var stats = await service.GetStatsAsync(admin.Id);
        Assert.NotNull(stats);
        Assert.True(stats!.WorkspaceCount >= 1);
        Assert.True(stats.UserCount >= 2);
        Assert.True(stats.SystemAdminCount >= 1);

        var workspaces = await service.GetWorkspacesAsync(admin.Id);
        Assert.NotNull(workspaces);
        var ws = Assert.Single(workspaces!, w => w.Id == created.Data!.Id);
        Assert.Equal("統計用WS", ws.Name);
        Assert.Equal(1, ws.MemberCount); // 作成者が ADMIN として1人
        Assert.False(ws.IsArchived);

        var users = await service.GetUsersAsync(admin.Id);
        Assert.NotNull(users);
        Assert.Contains(users!, u => u.Id == admin.Id && u.IsSystemAdmin);
        Assert.Contains(users!, u => u.Id == member.Id && !u.IsSystemAdmin);
    }
}
