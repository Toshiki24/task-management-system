using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Tests.Services;

public class FakeGitProviderTests
{
    private readonly FakeGitProvider _provider = new();

    private static GitWebhookRequest Request(string body, string? signature) =>
        new(signature is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["x-signature"] = signature }, body);

    [Fact(DisplayName = "M4 署名検証: 正しい HMAC は通り、改ざん・欠落は弾く")]
    public void VerifySignature()
    {
        const string secret = "s3cret";
        const string body = "{\"type\":\"push\"}";
        var good = FakeGitProvider.ComputeSignature(body, secret);

        Assert.True(_provider.VerifySignature(Request(body, good), secret));
        Assert.False(_provider.VerifySignature(Request(body, good + "00"), secret));
        Assert.False(_provider.VerifySignature(Request(body, null), secret));
        Assert.False(_provider.VerifySignature(Request("{\"type\":\"pr_merged\"}", good), secret));
    }

    [Fact(DisplayName = "M4 イベント正規化: 既知の type を共通イベントへ変換する")]
    public void ParseEvent_Normalizes()
    {
        var body = "{\"type\":\"pr_merged\",\"repoId\":\"r1\",\"repoFullName\":\"o/r\"," +
                   "\"ref\":\"feature/12-x\",\"title\":\"Closes #12\",\"actorId\":\"u1\",\"actorName\":\"octocat\"}";

        var ev = _provider.ParseEvent(Request(body, null));

        Assert.NotNull(ev);
        Assert.Equal(GitEventType.PrMerged, ev!.Type);
        Assert.Equal("o/r", ev.Repo.FullName);
        Assert.Equal("octocat", ev.Actor!.Username);
        Assert.Contains("12", ev.TaskHints);
    }

    [Fact(DisplayName = "M4 イベント正規化: 不正 JSON・未知 type・必須欠落は null")]
    public void ParseEvent_InvalidReturnsNull()
    {
        Assert.Null(_provider.ParseEvent(Request("not json", null)));
        Assert.Null(_provider.ParseEvent(Request("{\"type\":\"unknown\",\"repoId\":\"r1\",\"repoFullName\":\"o/r\"}", null)));
        Assert.Null(_provider.ParseEvent(Request("{\"type\":\"push\"}", null)));
    }

    [Fact(DisplayName = "M4 参照抽出: #123 / TASK-123 を重複なく拾う")]
    public void ExtractTaskHints()
    {
        var hints = FakeGitProvider.ExtractTaskHints("fix #12 and TASK-34, again #12");

        Assert.Equal(new[] { "12", "34" }, hints);
        Assert.Empty(FakeGitProvider.ExtractTaskHints("no refs here"));
    }
}
