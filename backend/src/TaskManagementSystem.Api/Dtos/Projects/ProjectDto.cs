namespace TaskManagementSystem.Api.Dtos.Projects;

public record ProjectDto(
    long Id,
    long WorkspaceId,
    string Name,
    string? Description,
    string Status,
    DateOnly? StartDate,
    DateOnly? EndDate
);
