namespace TaskManagementSystem.Api.Dtos.Labels;

public record LabelDto(long Id, long WorkspaceId, string Name, string? Color);
