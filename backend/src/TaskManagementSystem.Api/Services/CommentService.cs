using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Comments;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class CommentService : ICommentService
{
    private readonly AppDbContext _dbContext;

    public CommentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CommentDto>?> GetByTaskAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        // 削除済みも含めて時系列で返す(スレッドの連続性のため)。本文は削除済みなら伏せる(M3 §3)
        return await _dbContext.TaskComments
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.Id)
            .Select(c => new CommentDto(
                c.Id,
                c.TaskId,
                c.UserId,
                c.User.Name,
                c.DeletedAt != null ? null : c.Comment,
                c.Edited,
                c.DeletedAt != null,
                c.CommentMentions.Select(m => new MentionUserDto(m.UserId, m.User.Name)).ToList(),
                c.CreatedAt))
            .ToListAsync();
    }

    public async Task<CreateCommentOutcome> CreateAsync(long taskId, long userId, CommentRequest request)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, userId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateCommentOutcome(CreateCommentResult.TaskNotFound);
        }

        // Viewer はコメントを投稿できない
        if (!access.Value.CanWrite)
        {
            return new CreateCommentOutcome(CreateCommentResult.Forbidden);
        }

        var comment = new TaskComment
        {
            TaskId = taskId,
            UserId = userId,
            Comment = request.Comment,
        };

        _dbContext.TaskComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        // コメント投稿をアクティビティに記録する(M3 §5)
        var projectId = await _dbContext.Tasks
            .Where(t => t.Id == taskId).Select(t => t.ProjectId).FirstAsync();
        var excerpt = request.Comment.Length > 50 ? request.Comment[..50] : request.Comment;
        ActivityRecorder.Record(_dbContext, projectId, taskId, userId,
            ActivityVerb.Commented, new { commentId = comment.Id, excerpt });

        // @メンションを解決して保存する(M3 §3)
        var mentioned = await SyncMentionsAsync(projectId, comment.Id, request.Comment);
        // 自動ウォッチ: コメント投稿者と被メンション者をウォッチに追加する(M3 §4)
        await WatcherRecorder.EnsureWatchingAsync(_dbContext, taskId, mentioned.Append(userId));
        await _dbContext.SaveChangesAsync();

        return new CreateCommentOutcome(
            CreateCommentResult.Success,
            new CommentCreatedDto(comment.Id, comment.TaskId, comment.UserId, comment.Comment, comment.CreatedAt));
    }

    public async Task<UpdateCommentOutcome> UpdateAsync(
        long taskId, long commentId, long currentUserId, CommentRequest request)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateCommentOutcome(UpdateCommentResult.NotFound);
        }

        var comment = await _dbContext.TaskComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId && c.DeletedAt == null);
        if (comment is null)
        {
            return new UpdateCommentOutcome(UpdateCommentResult.NotFound);
        }

        // 編集は投稿者本人のみ(M3 §3)
        if (comment.UserId != currentUserId)
        {
            return new UpdateCommentOutcome(UpdateCommentResult.Forbidden);
        }

        comment.Comment = request.Comment;
        comment.Edited = true;

        // @メンションを再解決する(M3 §3)
        var projectId = await _dbContext.Tasks
            .Where(t => t.Id == taskId).Select(t => t.ProjectId).FirstAsync();
        var mentioned = await SyncMentionsAsync(projectId, comment.Id, request.Comment);
        // 新たに言及された人を自動ウォッチに追加する(M3 §4)
        await WatcherRecorder.EnsureWatchingAsync(_dbContext, taskId, mentioned);

        // UpdatedAt は SaveChanges 時に自動更新される
        await _dbContext.SaveChangesAsync();

        var name = await _dbContext.Users.Where(u => u.Id == comment.UserId).Select(u => u.Name).FirstAsync();
        var mentions = await _dbContext.CommentMentions
            .Where(m => m.CommentId == comment.Id)
            .Select(m => new MentionUserDto(m.UserId, m.User.Name))
            .ToListAsync();
        return new UpdateCommentOutcome(
            UpdateCommentResult.Success,
            new CommentDto(comment.Id, comment.TaskId, comment.UserId, name, comment.Comment,
                comment.Edited, false, mentions, comment.CreatedAt));
    }

    public async Task<DeleteCommentResult> DeleteAsync(long taskId, long commentId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteCommentResult.NotFound;
        }

        var comment = await _dbContext.TaskComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId && c.DeletedAt == null);
        if (comment is null)
        {
            return DeleteCommentResult.NotFound;
        }

        // 削除は投稿者本人、またはプロジェクト管理者(Project OWNER / WS Admin)(M3 §3)
        if (comment.UserId != currentUserId && !access.Value.CanManageProject)
        {
            return DeleteCommentResult.Forbidden;
        }

        comment.DeletedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _dbContext.SaveChangesAsync();
        return DeleteCommentResult.Success;
    }

    /// <summary>
    /// 本文中の「@表示名」を解決し、comment_mentions を付け替える(M3 §3)。
    /// 解決対象はそのプロジェクトのメンバーのみ(非メンバーは解決しない=存在を開示しない)。
    /// </summary>
    private async Task<HashSet<long>> SyncMentionsAsync(long projectId, long commentId, string body)
    {
        var members = await _dbContext.ProjectMembers
            .Where(pm => pm.ProjectId == projectId)
            .Select(pm => new MemberName(pm.UserId, pm.User.Name))
            .ToListAsync();

        var mentionedIds = ResolveMentionedUserIds(body, members);

        var existing = await _dbContext.CommentMentions.Where(m => m.CommentId == commentId).ToListAsync();
        _dbContext.CommentMentions.RemoveRange(existing);
        foreach (var userId in mentionedIds)
        {
            _dbContext.CommentMentions.Add(new CommentMention { CommentId = commentId, UserId = userId });
        }

        return mentionedIds;
    }

    private readonly record struct MemberName(long UserId, string Name);

    /// <summary>
    /// 本文から被メンションのユーザー ID を解決する。「@名前」の直後が英数字でない(=名前の途中でない)ものを採用。
    /// 長い名前を優先して前方一致の誤検出を抑える。
    /// </summary>
    private static HashSet<long> ResolveMentionedUserIds(string body, IEnumerable<MemberName> members)
    {
        var result = new HashSet<long>();
        if (string.IsNullOrEmpty(body))
        {
            return result;
        }

        foreach (var member in members.OrderByDescending(m => m.Name.Length))
        {
            if (string.IsNullOrEmpty(member.Name))
            {
                continue;
            }

            var token = "@" + member.Name;
            var idx = 0;
            while ((idx = body.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
            {
                var after = idx + token.Length;
                if (after >= body.Length || !char.IsLetterOrDigit(body[after]))
                {
                    result.Add(member.UserId);
                    break;
                }

                idx = after;
            }
        }

        return result;
    }
}
