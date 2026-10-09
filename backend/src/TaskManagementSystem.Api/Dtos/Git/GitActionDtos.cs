using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Git;

/// <summary>タスクからのブランチ作成(M4 §7)。</summary>
public record CreateBranchRequest(
    [Required(ErrorMessage = "連携リポジトリは必須です。")]
    long RepositoryLinkId,

    /// <summary>作成するブランチ名。未指定なら feature/{taskId}-{slug} を採番する。</summary>
    [MaxLength(200, ErrorMessage = "ブランチ名は200文字以内で入力してください。")]
    string? BranchName,

    /// <summary>派生元。未指定なら連携リポジトリの既定ブランチ(無ければ main)。</summary>
    [MaxLength(200, ErrorMessage = "派生元は200文字以内で入力してください。")]
    string? FromRef);

/// <summary>タスクからの PR/MR 作成(M4 §7)。</summary>
public record CreatePullRequestRequest(
    [Required(ErrorMessage = "連携リポジトリは必須です。")]
    long RepositoryLinkId,

    [Required(ErrorMessage = "ソースブランチは必須です。")]
    [MaxLength(200, ErrorMessage = "ソースブランチは200文字以内で入力してください。")]
    string SourceBranch,

    /// <summary>マージ先。未指定なら既定ブランチ(無ければ main)。</summary>
    [MaxLength(200, ErrorMessage = "マージ先は200文字以内で入力してください。")]
    string? TargetBranch,

    /// <summary>タイトル。未指定ならタスク名。</summary>
    [MaxLength(300, ErrorMessage = "タイトルは300文字以内で入力してください。")]
    string? Title);
