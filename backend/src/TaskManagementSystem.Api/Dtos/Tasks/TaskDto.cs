namespace TaskManagementSystem.Api.Dtos.Tasks;

public record TaskDto(
    long Id,
    long ProjectId,
    long? AssigneeId,
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateOnly? DueDate
);
