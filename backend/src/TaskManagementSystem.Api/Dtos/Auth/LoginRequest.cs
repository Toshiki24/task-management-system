using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Auth;

public record LoginRequest(
    [Required(ErrorMessage = "メールアドレスは必須です。")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
    string Email,

    [Required(ErrorMessage = "パスワードは必須です。")]
    string Password
);
