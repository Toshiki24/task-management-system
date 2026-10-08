using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Comments;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class CommentServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public CommentServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "UT-601 存在しないタスクのコメント一覧取得")]
    public async Task GetByTaskAsync_ReturnsNull_WhenTaskNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new CommentService(context).GetByTaskAsync(TestData.NonExistentId, user.Id);

        Assert.Null(result);
    }

    [Fact(DisplayName = "UT-602 コメント一覧に投稿者名が含まれる")]
    public async Task GetByTaskAsync_IncludesUserName()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        var user1 = await TestData.CreateUserAsync(arrange);
        var user2 = await TestData.CreateUserAsync(arrange);
        var comment1 = await TestData.CreateCommentAsync(arrange, task.Id, user1.Id);
        var comment2 = await TestData.CreateCommentAsync(arrange, task.Id, user2.Id);

        await using var context = _db.CreateContext();
        var result = await new CommentService(context).GetByTaskAsync(task.Id, owner.Id);

        Assert.NotNull(result);
        Assert.Collection(
            result,
            c => Assert.Equal((comment1.Id, user1.Name), (c.Id, c.UserName)),
            c => Assert.Equal((comment2.Id, user2.Name), (c.Id, c.UserName)));
    }

    [Fact(DisplayName = "UT-603 コメント登録成功")]
    public async Task CreateAsync_CreatesComment_WithGivenUserId()
    {
        await using var arrange = _db.CreateContext();
        var project = await TestData.CreateProjectAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        var user = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, user.Id);
        var request = new CommentRequest(TestData.Unique("ut-comment"));

        // コントローラーはJWTのsubクレームから取得したユーザーIDをそのまま渡す
        await using var context = _db.CreateContext();
        var outcome = await new CommentService(context).CreateAsync(task.Id, user.Id, request);

        Assert.Equal(CreateCommentResult.Success, outcome.Result);
        var created = outcome.Data!;
        Assert.Equal(task.Id, created.TaskId);
        Assert.Equal(user.Id, created.UserId);
        Assert.Equal(request.Comment, created.Comment);

        await using var assert = _db.CreateContext();
        var saved = await assert.TaskComments.SingleAsync(c => c.Id == created.Id);
        Assert.Equal(user.Id, saved.UserId);
    }

    [Fact(DisplayName = "UT-604 存在しないタスクへのコメント登録")]
    public async Task CreateAsync_ReturnsTaskNotFound_WhenTaskNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new CommentService(context)
            .CreateAsync(TestData.NonExistentId, user.Id, new CommentRequest("コメント"));

        Assert.Equal(CreateCommentResult.TaskNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-605 所属していないプロジェクトのコメント一覧取得・登録")]
    public async Task CommentOperations_ReturnNotFound_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        await TestData.CreateCommentAsync(arrange, task.Id, owner.Id);
        var outsider = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var service = new CommentService(context);
        var list = await service.GetByTaskAsync(task.Id, outsider.Id);
        var outcome = await service.CreateAsync(task.Id, outsider.Id, new CommentRequest("コメント"));

        Assert.Null(list);
        Assert.Equal(CreateCommentResult.TaskNotFound, outcome.Result);
        await using var assert = _db.CreateContext();
        Assert.False(await assert.TaskComments.AnyAsync(c => c.TaskId == task.Id && c.UserId == outsider.Id));
    }

    [Fact(DisplayName = "UT-606 Viewer はコメントを投稿できない(403)")]
    public async Task CreateAsync_ForbiddenForViewer()
    {
        await using var arrange = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        var workspaceId = await arrange.Projects.Where(p => p.Id == project.Id)
            .Select(p => p.WorkspaceId).SingleAsync();
        var viewer = await TestData.CreateUserAsync(arrange);
        await TestData.AddWorkspaceMemberAsync(arrange, workspaceId, viewer.Id, WorkspaceMemberRole.Viewer);

        await using var context = _db.CreateContext();
        var outcome = await new CommentService(context)
            .CreateAsync(task.Id, viewer.Id, new CommentRequest("コメント"));

        Assert.Equal(CreateCommentResult.Forbidden, outcome.Result);
    }

    [Fact(DisplayName = "M3 投稿者はコメントを編集でき、edited=true になる")]
    public async Task UpdateAsync_ByAuthor_SetsEdited()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var service = new CommentService(ctx);
        var created = (await service.CreateAsync(task.Id, owner.Id, new CommentRequest("最初"))).Data!;

        var outcome = await service.UpdateAsync(task.Id, created.Id, owner.Id, new CommentRequest("修正後"));

        Assert.Equal(UpdateCommentResult.Success, outcome.Result);
        Assert.Equal("修正後", outcome.Data!.Comment);
        Assert.True(outcome.Data.Edited);
    }

    [Fact(DisplayName = "M3 他人のコメントは編集できない(403)")]
    public async Task UpdateAsync_ByOther_Forbidden()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var service = new CommentService(ctx);
        var created = (await service.CreateAsync(task.Id, owner.Id, new CommentRequest("本人の"))).Data!;

        Assert.Equal(UpdateCommentResult.Forbidden,
            (await service.UpdateAsync(task.Id, created.Id, member.Id, new CommentRequest("横取り"))).Result);
    }

    [Fact(DisplayName = "M3 投稿者は論理削除でき、一覧では本文が伏せられる")]
    public async Task DeleteAsync_ByAuthor_MasksBody()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var service = new CommentService(ctx);
        var created = (await service.CreateAsync(task.Id, owner.Id, new CommentRequest("消えるコメント"))).Data!;

        Assert.Equal(DeleteCommentResult.Success, await service.DeleteAsync(task.Id, created.Id, owner.Id));

        var list = await service.GetByTaskAsync(task.Id, owner.Id);
        var dto = Assert.Single(list!);
        Assert.True(dto.IsDeleted);
        Assert.Null(dto.Comment);
    }

    [Fact(DisplayName = "M3 プロジェクト管理者(OWNER)は他人のコメントを削除できる")]
    public async Task DeleteAsync_ByManager_Allowed()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var service = new CommentService(ctx);
        // member が投稿、owner(プロジェクト OWNER / WS Admin)が削除
        var created = (await service.CreateAsync(task.Id, member.Id, new CommentRequest("メンバーの発言"))).Data!;

        Assert.Equal(DeleteCommentResult.Success, await service.DeleteAsync(task.Id, created.Id, owner.Id));
    }

    [Fact(DisplayName = "M3 一般メンバーは他人のコメントを削除できない(403)")]
    public async Task DeleteAsync_ByOtherMember_Forbidden()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var service = new CommentService(ctx);
        var created = (await service.CreateAsync(task.Id, owner.Id, new CommentRequest("OWNERの発言"))).Data!;

        // member は投稿者でも管理者でもないので削除不可
        Assert.Equal(DeleteCommentResult.Forbidden, await service.DeleteAsync(task.Id, created.Id, member.Id));
    }
}
