namespace TaskManagementSystem.Api.Dtos.Git;

/// <summary>タスクに紐づく Git リンク 1 件(ブランチ/PR/MR/コミット。M4 §7)。</summary>
public record TaskGitLinkDto(
    long Id,
    long TaskId,
    string LinkType,
    string ExternalRef,
    string? Url,
    string? Title,
    string? State);
