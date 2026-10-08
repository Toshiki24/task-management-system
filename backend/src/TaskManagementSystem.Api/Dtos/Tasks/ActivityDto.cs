using System.Text.Json;

namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>
/// アクティビティ 1 件(Phase 2 M3 §5)。Payload は種別ごとの付加情報(JSON)をそのまま返す。
/// </summary>
public record ActivityDto(
    long Id,
    long? TaskId,
    long ActorUserId,
    string ActorName,
    string Verb,
    JsonElement? Payload,
    DateTime CreatedAt);
