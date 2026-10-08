namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>
/// コマンドパレット等の横断検索で返すタスクの要約(M2 §5.5 コマンドパレット)。
/// 一覧用の軽量 DTO(ラベルや進捗は含めない)。
/// </summary>
public record TaskSearchResultDto(long Id, long ProjectId, string ProjectName, string Title, string Status);
