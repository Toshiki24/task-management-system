using System.Text;

namespace TaskManagementSystem.Api.Services.Csv;

/// <summary>
/// CSV の組み立てユーティリティ(M5 §4)。RFC 4180 準拠の引用に加え、表計算ソフトでの
/// 数式実行(CSV インジェクション)を防ぐため、危険な先頭文字を無害化する。
/// </summary>
public static class Csv
{
    /// <summary>行(セルの配列)を 1 本の CSV 行(CRLF 終端)に変換する。</summary>
    public static string Row(params string?[] cells) =>
        string.Join(",", cells.Select(EscapeCell)) + "\r\n";

    /// <summary>
    /// セルを安全な CSV フィールドにする。先頭が = + - @ やタブ/改行のセルは、数式として
    /// 解釈されないよう先頭にアポストロフィを付ける。カンマ・引用符・改行を含む場合は引用する。
    /// </summary>
    public static string EscapeCell(string? value)
    {
        var cell = value ?? string.Empty;

        if (cell.Length > 0 && cell[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            cell = "'" + cell;
        }

        if (cell.Contains(',') || cell.Contains('"') || cell.Contains('\n') || cell.Contains('\r'))
        {
            cell = "\"" + cell.Replace("\"", "\"\"") + "\"";
        }

        return cell;
    }

    /// <summary>UTF-8 BOM 付きのバイト列にする(Excel で文字化けしないようにする)。</summary>
    public static byte[] ToBomBytes(string content) =>
        Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
}
