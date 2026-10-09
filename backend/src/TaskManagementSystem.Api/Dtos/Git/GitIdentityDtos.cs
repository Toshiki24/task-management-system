using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Git;

/// <summary>Git ユーザー⇄アプリメンバーの対応付け 1 件(M4 §6)。表示用に本人の氏名/メールを含む。</summary>
public record GitIdentityDto(
    long Id,
    long WorkspaceId,
    long UserId,
    string UserName,
    string UserEmail,
    string Provider,
    string ExternalUserId,
    string? ExternalUsername);

/// <summary>対応付けの作成。</summary>
public record CreateGitIdentityRequest(
    [Required(ErrorMessage = "対象ユーザーは必須です。")]
    long UserId,

    [Required(ErrorMessage = "プロバイダは必須です。")]
    [AllowedValues(GitProvider.GitHub, GitProvider.GitLab, ErrorMessage = "プロバイダが不正です。")]
    string Provider,

    [Required(ErrorMessage = "Git ユーザーIDは必須です。")]
    [MaxLength(200, ErrorMessage = "Git ユーザーIDは200文字以内で入力してください。")]
    string ExternalUserId,

    [MaxLength(200, ErrorMessage = "Git ユーザー名は200文字以内で入力してください。")]
    string? ExternalUsername);
