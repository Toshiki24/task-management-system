namespace TaskManagementSystem.Api.Dtos.Metrics;

/// <summary>取り込みに失敗した行(1 始まりの行番号＝ヘッダ除く)とその理由。</summary>
public record ImportRowErrorDto(int Row, string Message);

/// <summary>CSV インポートの結果(M5 §4)。</summary>
public record ImportResultDto(int Imported, IReadOnlyList<ImportRowErrorDto> Failed);
