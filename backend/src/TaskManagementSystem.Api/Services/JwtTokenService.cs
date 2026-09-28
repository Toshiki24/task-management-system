using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AccessToken GenerateToken(User user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = jwtSection["Key"]
            ?? throw new InvalidOperationException("Jwt:Key が設定されていません。");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        // リフレッシュトークンで再発行する前提のため短命にする(security-review.md 5.3)。
        // E2Eテストで期限切れ時の動作を短時間で確認できるよう、小数(0.1分=6秒など)も指定できる
        var expiresMinutes = jwtSection.GetValue("ExpiresMinutes", 15d);
        var expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // ClaimTypes.NameはURI形式のクレーム名のまま出力されるため、JWT標準の "name" を使う
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        // expクレームは秒単位のため、呼び出し側に返す有効期限もトークンの値(ValidTo)に揃える
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), token.ValidTo);
    }
}
