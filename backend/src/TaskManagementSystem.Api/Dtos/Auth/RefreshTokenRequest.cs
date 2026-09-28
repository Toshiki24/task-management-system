using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Auth;

/// <summary>リフレッシュAPI・ログアウトAPIのリクエスト</summary>
public record RefreshTokenRequest(
    [Required(ErrorMessage = "リフレッシュトークンは必須です。")]
    string RefreshToken
);
