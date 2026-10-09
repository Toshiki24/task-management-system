using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Git;

/// <summary>Git 接続 1 件(M4 §4)。資格情報(secret_ref 含む)は返さない。</summary>
public record GitConnectionDto(
    long Id,
    long WorkspaceId,
    string Provider,
    string? BaseUrl,
    string AuthType,
    string? ExternalAccount,
    string Status);

/// <summary>Git 接続の作成。</summary>
public record CreateGitConnectionRequest(
    [Required(ErrorMessage = "プロバイダは必須です。")]
    [AllowedValues(GitProvider.GitHub, GitProvider.GitLab, ErrorMessage = "プロバイダが不正です。")]
    string Provider,

    [MaxLength(300, ErrorMessage = "ベースURLは300文字以内で入力してください。")]
    string? BaseUrl,

    [Required(ErrorMessage = "認証種別は必須です。")]
    [AllowedValues(GitAuthType.GitHubApp, GitAuthType.OAuth, GitAuthType.Pat, GitAuthType.GroupToken,
        ErrorMessage = "認証種別が不正です。")]
    string AuthType,

    [MaxLength(300, ErrorMessage = "シークレット参照は300文字以内で入力してください。")]
    string? SecretRef,

    [MaxLength(200, ErrorMessage = "アカウント名は200文字以内で入力してください。")]
    string? ExternalAccount);

/// <summary>Git 接続の更新(プロバイダは変更不可)。</summary>
public record UpdateGitConnectionRequest(
    [MaxLength(300, ErrorMessage = "ベースURLは300文字以内で入力してください。")]
    string? BaseUrl,

    [Required(ErrorMessage = "認証種別は必須です。")]
    [AllowedValues(GitAuthType.GitHubApp, GitAuthType.OAuth, GitAuthType.Pat, GitAuthType.GroupToken,
        ErrorMessage = "認証種別が不正です。")]
    string AuthType,

    [MaxLength(300, ErrorMessage = "シークレット参照は300文字以内で入力してください。")]
    string? SecretRef,

    [MaxLength(200, ErrorMessage = "アカウント名は200文字以内で入力してください。")]
    string? ExternalAccount,

    [Required(ErrorMessage = "状態は必須です。")]
    [AllowedValues(GitConnectionStatus.Active, GitConnectionStatus.Disabled, GitConnectionStatus.Error,
        ErrorMessage = "状態が不正です。")]
    string Status);
