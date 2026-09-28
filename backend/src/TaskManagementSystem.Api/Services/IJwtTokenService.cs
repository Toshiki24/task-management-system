using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <param name="Token">署名済みのJWT</param>
/// <param name="ExpiresAt">有効期限(UTC)</param>
public record AccessToken(string Token, DateTime ExpiresAt);

public interface IJwtTokenService
{
    AccessToken GenerateToken(User user);
}
