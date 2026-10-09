using System.Net;
using System.Net.Sockets;

namespace TaskManagementSystem.Api.Services.Git;

/// <summary>
/// self-managed GitLab 等のベース URL を検証し、内部ネットワークへの不正アクセス(SSRF)を防ぐ(M4 §13)。
/// https 限定、ホスト名の形式確認、ループバック/プライベート/リンクローカル IP の拒否を行う。
/// </summary>
public static class SsrfGuard
{
    /// <summary>ベース URL が外部接続先として許可できるか。</summary>
    public static bool IsAllowedBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // 資格情報を含む URL(user:pass@host)は拒否
        if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var host = uri.DnsSafeHost;
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        // ホスト名(例 gitlab.example.com)はそのまま許可。IP リテラルのみ範囲検査する。
        // (名前解決時の再バインド対策は接続層の責務。ここでは明らかな内部指定を弾く)
        if (IPAddress.TryParse(host, out var ip))
        {
            return !IsPrivateOrLoopback(ip);
        }

        // localhost 等の明示的な内部ホスト名を拒否
        return !host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            // 10.0.0.0/8、172.16.0.0/12、192.168.0.0/16、169.254.0.0/16(リンクローカル)、0.0.0.0
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)
                || b[0] == 0;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // リンクローカル(fe80::/10)・ユニークローカル(fc00::/7)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal
                || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }

        return true;
    }
}
