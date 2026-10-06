namespace TaskManagementSystem.Api.Dtos.Workflow;

public record WorkflowStateDto(
    long Id,
    long WorkspaceId,
    string Key,
    string Name,
    string Category,
    int Position,
    bool IsDefault,
    string? Color
);
