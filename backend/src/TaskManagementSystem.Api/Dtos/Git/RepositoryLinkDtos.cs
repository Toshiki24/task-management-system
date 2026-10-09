using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Git;

/// <summary>プロジェクトに連携されたリポジトリ 1 件(M4 §2)。</summary>
public record RepositoryLinkDto(
    long Id,
    long ProjectId,
    long GitConnectionId,
    string ExternalRepoId,
    string RepoFullName,
    string? DefaultBranch);

/// <summary>プロジェクトへのリポジトリ連携の作成。</summary>
public record CreateRepositoryLinkRequest(
    [Required(ErrorMessage = "接続は必須です。")]
    long GitConnectionId,

    [Required(ErrorMessage = "リポジトリIDは必須です。")]
    [MaxLength(200, ErrorMessage = "リポジトリIDは200文字以内で入力してください。")]
    string ExternalRepoId,

    [Required(ErrorMessage = "リポジトリ名は必須です。")]
    [MaxLength(300, ErrorMessage = "リポジトリ名は300文字以内で入力してください。")]
    string RepoFullName,

    [MaxLength(200, ErrorMessage = "既定ブランチは200文字以内で入力してください。")]
    string? DefaultBranch);
