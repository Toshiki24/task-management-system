using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Setup;

/// <summary>初回セットアップの状態。機密情報は含めず、必要かどうかの真偽のみを返す(M5 §8)。</summary>
public record SetupStatusDto(bool SetupRequired);

/// <summary>初回 System Admin の作成リクエスト。</summary>
public record CreateInitialAdminRequest(
    [Required(ErrorMessage = "名前は必須です。")]
    [MaxLength(100, ErrorMessage = "名前は100文字以内で入力してください。")]
    string Name,

    [Required(ErrorMessage = "メールアドレスは必須です。")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
    [MaxLength(255, ErrorMessage = "メールアドレスは255文字以内で入力してください。")]
    string Email,

    [Required(ErrorMessage = "パスワードは必須です。")]
    [MinLength(12, ErrorMessage = "パスワードは12文字以上で入力してください。")]
    [MaxLength(200, ErrorMessage = "パスワードは200文字以内で入力してください。")]
    string Password
);
