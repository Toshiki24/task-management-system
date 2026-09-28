using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Comments;
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
        var created = await new CommentService(context).CreateAsync(task.Id, user.Id, request);

        Assert.NotNull(created);
        Assert.Equal(task.Id, created.TaskId);
        Assert.Equal(user.Id, created.UserId);
        Assert.Equal(request.Comment, created.Comment);

        await using var assert = _db.CreateContext();
        var saved = await assert.TaskComments.SingleAsync(c => c.Id == created.Id);
        Assert.Equal(user.Id, saved.UserId);
    }

    [Fact(DisplayName = "UT-604 存在しないタスクへのコメント登録")]
    public async Task CreateAsync_ReturnsNull_WhenTaskNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var created = await new CommentService(context)
            .CreateAsync(TestData.NonExistentId, user.Id, new CommentRequest("コメント"));

        Assert.Null(created);
    }

    [Fact(DisplayName = "UT-605 所属していないプロジェクトのコメント一覧取得・登録")]
    public async Task CommentOperations_ReturnNull_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        await TestData.CreateCommentAsync(arrange, task.Id, owner.Id);
        var outsider = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var service = new CommentService(context);
        var list = await service.GetByTaskAsync(task.Id, outsider.Id);
        var created = await service.CreateAsync(task.Id, outsider.Id, new CommentRequest("コメント"));

        Assert.Null(list);
        Assert.Null(created);
        await using var assert = _db.CreateContext();
        Assert.False(await assert.TaskComments.AnyAsync(c => c.TaskId == task.Id && c.UserId == outsider.Id));
    }
}
