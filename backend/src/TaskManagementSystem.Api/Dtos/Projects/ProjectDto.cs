namespace TaskManagementSystem.Api.Dtos.Projects;

public record ProjectDto(
    long Id,
    string Name,
    string? Description,
    string Status,
    DateOnly? StartDate,
    DateOnly? EndDate
);
