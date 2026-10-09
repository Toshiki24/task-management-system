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

    /// <summary>
    /// CSV テキストを行(セル配列)の並びに解析する(RFC 4180・引用/二重引用符/改行対応)。
    /// 先頭の BOM は除去する。空行はスキップする。
    /// </summary>
    public static List<string[]> Parse(string content)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(content))
        {
            return rows;
        }

        // 先頭 BOM を除去
        if (content[0] == '﻿')
        {
            content = content[1..];
        }

        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void EndField() { fields.Add(field.ToString()); field.Clear(); }
        void EndRow()
        {
            EndField();
            // 全セルが空の行はスキップ(末尾改行対策)
            if (fields.Any(f => f.Length > 0))
            {
                rows.Add(fields.ToArray());
            }
            fields = new List<string>();
        }

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                EndField();
            }
            else if (c == '\r')
            {
                // CRLF/CR を行区切りとして扱う(次が \n ならまとめて消費)
                if (i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++;
                }
                EndRow();
            }
            else if (c == '\n')
            {
                EndRow();
            }
            else
            {
                field.Append(c);
            }
        }

        // 末尾(改行で終わっていない最終行)を確定
        if (field.Length > 0 || fields.Count > 0)
        {
            EndRow();
        }

        return rows;
    }
}
