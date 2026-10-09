namespace TaskManagementSystem.Api.Models;

/// <summary>
/// プロジェクト⇄リポジトリの対応(Phase 2 M4 §2)。1 プロジェクトに複数リポジトリを連携できる。
/// 接続(資格情報・プロバイダ)は <see cref="GitConnection"/> を介して解決する。
/// </summary>
public class RepositoryLink
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long GitConnectionId { get; set; }

    /// <summary>プロバイダ側の安定したリポジトリ ID(リネーム耐性のため名前と別に持つ)。</summary>
    public string ExternalRepoId { get; set; } = null!;

    /// <summary>表示用のリポジトリ名(owner/repo)。</summary>
    public string RepoFullName { get; set; } = null!;

    public string? DefaultBranch { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public GitConnection GitConnection { get; set; } = null!;
}
