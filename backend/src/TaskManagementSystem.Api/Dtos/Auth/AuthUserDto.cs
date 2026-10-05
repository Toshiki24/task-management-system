namespace TaskManagementSystem.Api.Dtos.Auth;

/// <summary>
/// ログイン/リフレッシュ応答に含めるユーザー情報。画面の権限制御のため <see cref="IsSystemAdmin"/> を含む。
/// (一般のユーザー一覧 API の <c>UserDto</c> はこの情報を公開しない)
/// </summary>
public record AuthUserDto(long Id, string Name, string Email, bool IsSystemAdmin);
