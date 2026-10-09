using System.Text.RegularExpressions;

namespace TaskManagementSystem.Api.Services.Git;

/// <summary>
/// コミット/PR/MR・ブランチ名からタスク参照(#123 / TASK-123)を抽出する(M4 §7)。
/// 各プロバイダのアダプタ(GitHub/GitLab/Fake)で共有する。
/// </summary>
public static class GitTaskHints
{
    private static readonly Regex Pattern = new(@"(?:#|TASK-)(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<string> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return Pattern.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}
