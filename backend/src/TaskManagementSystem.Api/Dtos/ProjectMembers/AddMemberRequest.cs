using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.ProjectMembers;

public record AddMemberRequest(
    [Required(ErrorMessage = "ユーザーIDは必須です。")]
    long? UserId,

    [Required(ErrorMessage = "プロジェクト内権限は必須です。")]
    [AllowedValues(null, ProjectMemberRole.Owner, ProjectMemberRole.Member,
        ErrorMessage = "プロジェクト内権限の値が不正です。")]
    string? Role
);
