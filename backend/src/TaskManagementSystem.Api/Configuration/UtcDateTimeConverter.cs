using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskManagementSystem.Api.Configuration;

/// <summary>
/// DateTime を常に UTC の ISO 8601(末尾 Z)でシリアライズする。
/// DB の timestamp without time zone は UTC で保存しているが、EF からは Kind=Unspecified で
/// 返るため、そのままだと Z が付かず、フロントでローカル時刻と誤解釈される(例: コメントの時刻が
/// UTC のまま表示される)。UTC であることを明示して返す。
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        // Kind=Utc の DateTime は System.Text.Json が末尾 Z 付きの ISO 8601 で書き出す
        writer.WriteStringValue(utc);
    }
}
