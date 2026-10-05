using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Comments;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/comments")]
public class TaskCommentsController : ControllerBase
{
    private readonly ICommentService _commentService;

    public TaskCommentsController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CommentDto>>> GetAll(long taskId)
    {
        var comments = await _commentService.GetByTaskAsync(taskId, this.GetCurrentUserId());
        if (comments is null)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(comments);
    }

    [HttpPost]
    public async Task<ActionResult<CommentCreatedDto>> Create(long taskId, CommentRequest request)
    {
        var userId = this.GetCurrentUserId();

        var outcome = await _commentService.CreateAsync(taskId, userId, request);

        return outcome.Result switch
        {
            CreateCommentResult.TaskNotFound =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),

            CreateCommentResult.Forbidden => this.ForbiddenError(),

            _ => CreatedAtAction(nameof(GetAll), new { taskId }, outcome.Data),
        };
    }
}
