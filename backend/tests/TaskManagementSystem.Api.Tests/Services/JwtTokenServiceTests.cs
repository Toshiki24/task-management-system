using System.IdentityModel.Tokens.Jwt;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Tests.Services;

// DBを使わないためフィクスチャは不要
public class JwtTokenServiceTests
{
    private static readonly User TestUser = new()
    {
        Id = 42,
        Name = "テストユーザー",
        Email = "jwt-test@example.test",
        PasswordHash = "unused",
    };

    [Fact(DisplayName = "UT-701 トークンにユーザー情報が含まれる")]
    public void GenerateToken_ContainsUserClaims()
    {
        var token = new JwtTokenService(JwtTestConfiguration.Create()).GenerateToken(TestUser);

        // jwt.io等と同様に、ペイロードに実際に書き込まれたクレーム名で検証する
        var payload = new JwtSecurityTokenHandler().ReadJwtToken(token).Payload;
        Assert.Equal("42", payload[JwtRegisteredClaimNames.Sub]);
        Assert.Equal(TestUser.Email, payload[JwtRegisteredClaimNames.Email]);
        Assert.True(
            payload.TryGetValue(JwtRegisteredClaimNames.Name, out var name),
            $"nameクレームがありません。実際のクレーム: {string.Join(", ", payload.Keys)}");
        Assert.Equal(TestUser.Name, name);
    }

    [Fact(DisplayName = "UT-702 有効期限が設定に従っている")]
    public void GenerateToken_ExpiresAfterConfiguredMinutes()
    {
        const int expiresMinutes = 15;
        // expクレームは秒単位のため、前後を秒に丸めて比較する
        var issuedFrom = DateTime.UtcNow.AddSeconds(-1);

        var token = new JwtTokenService(JwtTestConfiguration.Create(expiresMinutes)).GenerateToken(TestUser);

        var issuedTo = DateTime.UtcNow.AddSeconds(1);
        var expiresAt = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
        Assert.InRange(expiresAt, issuedFrom.AddMinutes(expiresMinutes), issuedTo.AddMinutes(expiresMinutes));
    }
}
