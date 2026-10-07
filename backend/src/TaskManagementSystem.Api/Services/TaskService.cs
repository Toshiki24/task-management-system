using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Labels;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext _dbContext;

    public TaskService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TaskDto>?> GetByProjectAsync(long projectId, long currentUserId, TaskListQuery? query = null)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var tasks = await ApplySort(
                ApplyFilters(_dbContext.Tasks.Where(t => t.ProjectId == projectId), query, currentUserId),
                query?.Sort)
            .Include(t => t.TaskLabels).ThenInclude(tl => tl.Label)
            .ToListAsync();

        return tasks.Select(ToDto).ToList();
    }

    public async Task<PagedResult<MyTaskDto>> GetMyTasksAsync(long currentUserId, MyTasksQuery query)
    {
        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize.Value : 20;

        // 自分が担当で、かつ自分が所属する(見える)ワークスペースのタスクに限る(M2 §6)
        var q = _dbContext.Tasks
            .Where(t => t.AssigneeId == currentUserId
                && t.Project.Workspace.Members.Any(m => m.UserId == currentUserId));

        if (query.Status is { Length: > 0 })
        {
            q = q.Where(t => query.Status.Contains(t.Status));
        }

        if (query.Priority is { Length: > 0 })
        {
            q = q.Where(t => query.Priority.Contains(t.Priority));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var pattern = $"%{query.Keyword.Trim()}%";
            q = q.Where(t =>
                EF.Functions.ILike(t.Title, pattern)
                || (t.Description != null && EF.Functions.ILike(t.Description, pattern)));
        }

        var total = await q.CountAsync();

        // 既定は期限の近い順(Postgres は ASC で NULL を末尾に置く)。指定時はホワイトリストの並べ替え。
        var sorted = string.IsNullOrWhiteSpace(query.Sort)
            ? q.OrderBy(t => t.DueDate).ThenBy(t => t.Id)
            : ApplySort(q, query.Sort);

        var items = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.Project).ThenInclude(p => p.Workspace)
            .Include(t => t.TaskLabels).ThenInclude(tl => tl.Label)
            .ToListAsync();

        var dtos = items.Select(t => new MyTaskDto(
            t.Id,
            t.ProjectId,
            t.Project.Name,
            t.Project.WorkspaceId,
            t.Project.Workspace.Name,
            t.Title,
            t.Status,
            t.Priority,
            t.DueDate,
            t.EstimatePoints,
            t.TaskLabels
                .Where(tl => tl.Label is not null)
                .Select(tl => new LabelDto(tl.Label.Id, tl.Label.WorkspaceId, tl.Label.Name, tl.Label.Color))
                .OrderBy(l => l.Name)
                .ToList())).ToList();

        return new PagedResult<MyTaskDto>(dtos, page, pageSize, total);
    }

    /// <summary>検索・絞り込み条件を LINQ で積み上げる(M2 §5.1。生 SQL は使わない)。</summary>
    private static IQueryable<TaskItem> ApplyFilters(IQueryable<TaskItem> source, TaskListQuery? q, long currentUserId)
    {
        if (q is null)
        {
            return source;
        }

        if (q.Status is { Length: > 0 })
        {
            source = source.Where(t => q.Status.Contains(t.Status));
        }

        if (q.Priority is { Length: > 0 })
        {
            source = source.Where(t => q.Priority.Contains(t.Priority));
        }

        if (q.LabelId is { Length: > 0 })
        {
            source = source.Where(t => t.TaskLabels.Any(tl => q.LabelId.Contains(tl.LabelId)));
        }

        if (!string.IsNullOrWhiteSpace(q.AssigneeId))
        {
            if (q.AssigneeId == "none")
            {
                source = source.Where(t => t.AssigneeId == null);
            }
            else if (q.AssigneeId == "me")
            {
                source = source.Where(t => t.AssigneeId == currentUserId);
            }
            else if (long.TryParse(q.AssigneeId, out var assigneeId))
            {
                source = source.Where(t => t.AssigneeId == assigneeId);
            }
        }

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var pattern = $"%{q.Keyword.Trim()}%";
            source = source.Where(t =>
                EF.Functions.ILike(t.Title, pattern)
                || (t.Description != null && EF.Functions.ILike(t.Description, pattern)));
        }

        if (q.DueBefore is { } before)
        {
            source = source.Where(t => t.DueDate != null && t.DueDate <= before);
        }

        if (q.DueAfter is { } after)
        {
            source = source.Where(t => t.DueDate != null && t.DueDate >= after);
        }

        return source;
    }

    /// <summary>並べ替えを適用する。許可したキーのみ(ホワイトリスト)。既定は ID 昇順(従来互換)。</summary>
    private static IQueryable<TaskItem> ApplySort(IQueryable<TaskItem> source, string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return source.OrderBy(t => t.Id);
        }

        var desc = sort.StartsWith('-');
        var key = desc ? sort[1..] : sort;

        return key switch
        {
            "dueDate" => desc ? source.OrderByDescending(t => t.DueDate) : source.OrderBy(t => t.DueDate),
            "priority" => desc ? source.OrderByDescending(t => t.Priority) : source.OrderBy(t => t.Priority),
            "title" => desc ? source.OrderByDescending(t => t.Title) : source.OrderBy(t => t.Title),
            "status" => desc ? source.OrderByDescending(t => t.Status) : source.OrderBy(t => t.Status),
            "createdAt" => desc ? source.OrderByDescending(t => t.CreatedAt) : source.OrderBy(t => t.CreatedAt),
            "updatedAt" => desc ? source.OrderByDescending(t => t.UpdatedAt) : source.OrderBy(t => t.UpdatedAt),
            _ => source.OrderBy(t => t.Id),
        };
    }

    public async Task<TaskDto?> GetByIdAsync(long id, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var task = await _dbContext.Tasks
            .Include(t => t.TaskLabels).ThenInclude(tl => tl.Label)
            .FirstOrDefaultAsync(t => t.Id == id);
        return task is null ? null : ToDto(task);
    }

    public async Task<CreateTaskOutcome> CreateAsync(long projectId, TaskRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateTaskOutcome(CreateTaskResult.ProjectNotFound);
        }

        // Viewer はタスクを作成できない
        if (!access.Value.CanWrite)
        {
            return new CreateTaskOutcome(CreateTaskResult.Forbidden);
        }

        switch (await CheckAssigneeAsync(projectId, request.AssigneeId))
        {
            case AssigneeCheck.NotFound:
                return new CreateTaskOutcome(CreateTaskResult.AssigneeNotFound);
            case AssigneeCheck.NotMember:
                return new CreateTaskOutcome(CreateTaskResult.AssigneeNotMember);
        }

        // status はワークスペースのワークフロー(workflow_states)に対して検証する(M2 §3.2)。
        // 未指定なら既定状態(is_default)にする。固定の TODO/IN_PROGRESS/DONE には縛らない。
        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.WorkspaceId)
            .FirstAsync();
        var (validKeys, defaultKey) = await GetWorkflowAsync(workspaceId);

        if (request.Status is not null && !validKeys.Contains(request.Status))
        {
            return new CreateTaskOutcome(CreateTaskResult.InvalidStatus);
        }

        if (!await LabelsValidAsync(workspaceId, request.LabelIds))
        {
            return new CreateTaskOutcome(CreateTaskResult.InvalidLabel);
        }

        var status = request.Status ?? defaultKey ?? TaskItemStatus.Todo;

        var task = new TaskItem
        {
            ProjectId = projectId,
            AssigneeId = request.AssigneeId,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            Status = status,
            EstimatePoints = request.EstimatePoints,
            // 作成時はその状態列の末尾に置く
            BoardPosition = await NextBoardPositionAsync(projectId, status),
        };

        if (request.Priority is not null)
        {
            task.Priority = request.Priority;
        }

        _dbContext.Tasks.Add(task);
        await _dbContext.SaveChangesAsync();

        if (request.LabelIds is not null)
        {
            await SetTaskLabelsAsync(task, request.LabelIds);
            await _dbContext.SaveChangesAsync();
        }

        await LoadLabelsAsync(task);
        return new CreateTaskOutcome(CreateTaskResult.Success, ToDto(task));
    }

    public async Task<UpdateTaskOutcome> UpdateAsync(long id, TaskRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateTaskOutcome(UpdateTaskResult.TaskNotFound);
        }

        // Viewer はタスクを編集できない
        if (!access.Value.CanWrite)
        {
            return new UpdateTaskOutcome(UpdateTaskResult.Forbidden);
        }

        var task = await _dbContext.Tasks.FindAsync(id);
        if (task is null)
        {
            return new UpdateTaskOutcome(UpdateTaskResult.TaskNotFound);
        }

        switch (await CheckAssigneeAsync(task.ProjectId, request.AssigneeId))
        {
            case AssigneeCheck.NotFound:
                return new UpdateTaskOutcome(UpdateTaskResult.AssigneeNotFound);
            case AssigneeCheck.NotMember:
                return new UpdateTaskOutcome(UpdateTaskResult.AssigneeNotMember);
        }

        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == task.ProjectId)
            .Select(p => p.WorkspaceId)
            .FirstAsync();

        // status が指定された場合は、タスクの属するワークスペースのワークフローに対して検証する(M2 §3.2)
        if (request.Status is not null)
        {
            var (validKeys, _) = await GetWorkflowAsync(workspaceId);
            if (!validKeys.Contains(request.Status))
            {
                return new UpdateTaskOutcome(UpdateTaskResult.InvalidStatus);
            }
        }

        if (!await LabelsValidAsync(workspaceId, request.LabelIds))
        {
            return new UpdateTaskOutcome(UpdateTaskResult.InvalidLabel);
        }

        task.AssigneeId = request.AssigneeId;
        task.Title = request.Title;
        task.Description = request.Description;
        task.DueDate = request.DueDate;
        // 見積は NULL 許容なので、未指定(null)はそのまま解除として上書きする(PUTの完全上書きセマンティクス)
        task.EstimatePoints = request.EstimatePoints;

        // status/priorityのようなNOT NULL制約付きの列は、
        // 未指定(null)の場合に空にできないため既存値を維持する。
        if (request.Status is not null && request.Status != task.Status)
        {
            RecordStatusHistory(task, task.Status, request.Status, currentUserId);
            task.Status = request.Status;
        }

        if (request.Priority is not null)
        {
            task.Priority = request.Priority;
        }

        // ラベルは null=変更なし、配列指定=その集合で完全置換(空配列は全解除。M2 §2.3)
        if (request.LabelIds is not null)
        {
            await SetTaskLabelsAsync(task, request.LabelIds);
        }

        await _dbContext.SaveChangesAsync();

        await LoadLabelsAsync(task);
        return new UpdateTaskOutcome(UpdateTaskResult.Success, ToDto(task));
    }

    public async Task<DeleteTaskResult> DeleteAsync(long id, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteTaskResult.TaskNotFound;
        }

        // タスクの削除は WS Admin / Member が可能(Viewer は不可。M1 §3.3)
        if (!access.Value.CanWrite)
        {
            return DeleteTaskResult.Forbidden;
        }

        var task = await _dbContext.Tasks.FindAsync(id);
        if (task is null)
        {
            return DeleteTaskResult.TaskNotFound;
        }

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync();

        return DeleteTaskResult.Success;
    }

    public async Task<MoveTaskOutcome> MoveAsync(long id, MoveTaskRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new MoveTaskOutcome(MoveTaskResult.TaskNotFound);
        }

        // Viewer はカード移動できない
        if (!access.Value.CanWrite)
        {
            return new MoveTaskOutcome(MoveTaskResult.Forbidden);
        }

        var task = await _dbContext.Tasks.FindAsync(id);
        if (task is null)
        {
            return new MoveTaskOutcome(MoveTaskResult.TaskNotFound);
        }

        // 移動先の状態はそのタスクの属する WS のワークフローに含まれること(M2 §3.2)
        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == task.ProjectId)
            .Select(p => p.WorkspaceId)
            .FirstAsync();
        var (validKeys, _) = await GetWorkflowAsync(workspaceId);
        if (!validKeys.Contains(request.ToStatus))
        {
            return new MoveTaskOutcome(MoveTaskResult.InvalidStatus);
        }

        // 状態が変わるなら履歴に記録する(既存の task_status_histories を使う。M2 §4.2)
        if (request.ToStatus != task.Status)
        {
            RecordStatusHistory(task, task.Status, request.ToStatus, currentUserId);
            task.Status = request.ToStatus;
        }

        task.BoardPosition = await ResolveBoardPositionAsync(task, request.BeforeTaskId);

        await _dbContext.SaveChangesAsync();

        await LoadLabelsAsync(task);
        return new MoveTaskOutcome(MoveTaskResult.Success, ToDto(task));
    }

    /// <summary>その状態列の末尾(既存最大の次)の並び順を返す。</summary>
    private async Task<double> NextBoardPositionAsync(long projectId, string status)
    {
        var max = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId && t.Status == status)
            .Select(t => (double?)t.BoardPosition)
            .MaxAsync();
        return (max ?? -1d) + 1d;
    }

    /// <summary>
    /// 移動後の並び順を算出する。beforeTaskId が null なら列の末尾、指定ありならその直前(前のカードとの中点)。
    /// 自分自身は計算対象から除外する。
    /// </summary>
    private async Task<double> ResolveBoardPositionAsync(TaskItem task, long? beforeTaskId)
    {
        // 対象列(移動後の status)のカードを並び順で取得(自分は除く)
        var column = await _dbContext.Tasks
            .Where(t => t.ProjectId == task.ProjectId && t.Status == task.Status && t.Id != task.Id)
            .OrderBy(t => t.BoardPosition)
            .ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.BoardPosition })
            .ToListAsync();

        if (beforeTaskId is null)
        {
            // 末尾へ
            var last = column.Count > 0 ? column[^1].BoardPosition : -1d;
            return last + 1d;
        }

        var index = column.FindIndex(c => c.Id == beforeTaskId.Value);
        if (index < 0)
        {
            // 基準カードが見つからない(別列など)場合は末尾へ
            var last = column.Count > 0 ? column[^1].BoardPosition : -1d;
            return last + 1d;
        }

        var after = column[index].BoardPosition;
        var before = index == 0 ? after - 1d : column[index - 1].BoardPosition;
        return (before + after) / 2d;
    }

    /// <summary>状態遷移を task_status_histories に 1 件記録する(保存は呼び出し側の SaveChanges に委ねる)。</summary>
    private void RecordStatusHistory(TaskItem task, string fromStatus, string toStatus, long changedBy)
    {
        _dbContext.TaskStatusHistories.Add(new TaskStatusHistory
        {
            TaskId = task.Id,
            ChangedBy = changedBy,
            FromStatus = fromStatus,
            ToStatus = toStatus,
        });
    }

    /// <summary>
    /// ワークスペースのワークフロー状態キー一覧と既定キーを返す(M2 §3.2)。
    /// 状態が未設定の WS でも落ちないよう、既定が無ければ先頭、それも無ければ null を返す。
    /// </summary>
    private async Task<(List<string> ValidKeys, string? DefaultKey)> GetWorkflowAsync(long workspaceId)
    {
        var states = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.Position)
            .ThenBy(s => s.Id)
            .Select(s => new { s.Key, s.IsDefault })
            .ToListAsync();

        var validKeys = states.Select(s => s.Key).ToList();
        var defaultKey = states.FirstOrDefault(s => s.IsDefault)?.Key ?? validKeys.FirstOrDefault();
        return (validKeys, defaultKey);
    }

    private enum AssigneeCheck
    {
        Ok,
        NotFound,
        NotMember,
    }

    /// <summary>担当者はプロジェクトのメンバーに限定する(未割り当ては可)</summary>
    private async Task<AssigneeCheck> CheckAssigneeAsync(long projectId, long? assigneeId)
    {
        if (assigneeId is null)
        {
            return AssigneeCheck.Ok;
        }

        if (!await _dbContext.Users.AnyAsync(u => u.Id == assigneeId))
        {
            return AssigneeCheck.NotFound;
        }

        return await _dbContext.GetProjectRoleAsync(projectId, assigneeId.Value) is null
            ? AssigneeCheck.NotMember
            : AssigneeCheck.Ok;
    }

    private static TaskDto ToDto(TaskItem task) => new(
        task.Id,
        task.ProjectId,
        task.AssigneeId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.DueDate,
        task.BoardPosition,
        task.EstimatePoints,
        task.TaskLabels
            .Where(tl => tl.Label is not null)
            .Select(tl => new LabelDto(tl.Label.Id, tl.Label.WorkspaceId, tl.Label.Name, tl.Label.Color))
            .OrderBy(l => l.Name)
            .ToList());

    /// <summary>ラベルの付与集合がすべて対象ワークスペースのラベルであることを検証する。</summary>
    private async Task<bool> LabelsValidAsync(long workspaceId, long[]? labelIds)
    {
        if (labelIds is null || labelIds.Length == 0)
        {
            return true;
        }

        var distinct = labelIds.Distinct().ToList();
        var count = await _dbContext.Labels
            .CountAsync(l => l.WorkspaceId == workspaceId && distinct.Contains(l.Id));
        return count == distinct.Count;
    }

    /// <summary>タスクのラベルを指定集合で完全置換する(task_labels を入れ替える)。</summary>
    private async Task SetTaskLabelsAsync(TaskItem task, long[] labelIds)
    {
        await _dbContext.Entry(task).Collection(t => t.TaskLabels).LoadAsync();
        task.TaskLabels.Clear();
        foreach (var labelId in labelIds.Distinct())
        {
            task.TaskLabels.Add(new TaskLabel { TaskId = task.Id, LabelId = labelId });
        }
    }

    /// <summary>ToDto 用に task_labels とラベル本体を読み込む。</summary>
    private Task LoadLabelsAsync(TaskItem task) =>
        _dbContext.Entry(task).Collection(t => t.TaskLabels).Query().Include(tl => tl.Label).LoadAsync();
}
