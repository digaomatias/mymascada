using System.Text;
using System.Text.RegularExpressions;

namespace MyMascada.Infrastructure.Services.Telegram;

public static class TelegramMarkdownConverter
{
    // Characters that must be escaped in MarkdownV2 (outside of code blocks)
    private static readonly char[] SpecialChars = ['_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'];

    public static string ConvertToTelegramMarkdown(string markdown)
    {
        try
        {
            return ConvertInternal(markdown);
        }
        catch
        {
            // Fall back to escaped plain text on any parse error
            return EscapeMarkdownV2(markdown);
        }
    }

    private static string ConvertInternal(string markdown)
    {
        var lines = markdown.Split('\n');
        var result = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Preserve code blocks as-is (they don't need escaping inside)
            if (line.TrimStart().StartsWith("```"))
            {
                result.AppendLine(line);
                continue;
            }

            // Convert headers to bold
            var headerMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (headerMatch.Success)
            {
                var headerText = headerMatch.Groups[2].Value;
                result.AppendLine($"*{EscapeMarkdownV2(headerText)}*");
                result.AppendLine();
                continue;
            }

            // Process inline formatting
            line = ConvertInlineFormatting(line);

            result.AppendLine(line);
        }

        return result.ToString().TrimEnd();
    }

    private static string ConvertInlineFormatting(string line)
    {
        // Protect inline code first (backtick-wrapped content stays as-is)
        var segments = SplitByInlineCode(line);
        var result = new StringBuilder();

        foreach (var (text, isCode) in segments)
        {
            if (isCode)
            {
                result.Append('`');
                result.Append(text);
                result.Append('`');
            }
            else
            {
                var processed = text;

                // Extract bold/italic patterns before escaping
                // Bold: **text** → *text*
                processed = Regex.Replace(processed, @"\*\*(.+?)\*\*", m => $"*{EscapeMarkdownV2(m.Groups[1].Value)}*");
                // Italic: *text* or _text_ → _text_
                processed = Regex.Replace(processed, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", m => $"_{EscapeMarkdownV2(m.Groups[1].Value)}_");
                // Links: [text](url) → [text](url) (already MarkdownV2 format, just escape the text)
                processed = Regex.Replace(processed, @"\[(.+?)\]\((.+?)\)", m => $"[{EscapeMarkdownV2(m.Groups[1].Value)}]({m.Groups[2].Value})");

                // Escape remaining special chars in non-formatted text
                // We need to be careful not to double-escape already processed parts
                if (!processed.Contains('*') && !processed.Contains('_') && !processed.Contains('['))
                {
                    processed = EscapeMarkdownV2(processed);
                }
                else
                {
                    // Escape only chars that aren't part of formatting
                    processed = EscapeNonFormattingChars(processed);
                }

                result.Append(processed);
            }
        }

        return result.ToString();
    }

    private static List<(string text, bool isCode)> SplitByInlineCode(string line)
    {
        var segments = new List<(string text, bool isCode)>();
        var parts = line.Split('`');

        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0 || i % 2 == 1)
            {
                segments.Add((parts[i], i % 2 == 1));
            }
        }

        if (segments.Count == 0)
        {
            segments.Add((line, false));
        }

        return segments;
    }

    public static string EscapeMarkdownV2(string text)
    {
        var sb = new StringBuilder(text.Length * 2);
        foreach (var c in text)
        {
            if (Array.IndexOf(SpecialChars, c) >= 0)
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string EscapeNonFormattingChars(string text)
    {
        // Escape special chars but preserve *, _, [, ], (, ) used for formatting
        var formattingChars = new HashSet<char> { '*', '_', '[', ']', '(', ')' };
        var sb = new StringBuilder(text.Length * 2);
        foreach (var c in text)
        {
            if (Array.IndexOf(SpecialChars, c) >= 0 && !formattingChars.Contains(c))
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
