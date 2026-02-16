using System.Text;
using System.Text.RegularExpressions;

namespace MyMascada.Infrastructure.Services.Telegram;

/// <summary>
/// Converts GitHub-flavored markdown (from AI responses) to Telegram-compatible HTML.
/// Telegram HTML supports: b, i, code, pre, a, s, u, blockquote.
/// Only &lt; &gt; &amp; need entity-encoding — far more reliable than MarkdownV2.
/// </summary>
public static partial class TelegramMarkdownConverter
{
    public static string ConvertToTelegramHtml(string markdown)
    {
        try
        {
            return ConvertInternal(markdown);
        }
        catch
        {
            // Fall back to HTML-escaped plain text on any parse error
            return EscapeHtml(markdown);
        }
    }

    private static string ConvertInternal(string markdown)
    {
        var lines = markdown.Split('\n');
        var result = new StringBuilder();
        var inCodeBlock = false;
        var inBlockquote = false;
        var blockquoteLines = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Handle fenced code blocks
            if (trimmed.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    // Closing code block
                    result.AppendLine("</pre>");
                    inCodeBlock = false;
                }
                else
                {
                    // Flush any pending blockquote
                    FlushBlockquote(result, blockquoteLines, ref inBlockquote);

                    // Opening code block — extract optional language tag (ignored by Telegram)
                    result.AppendLine("<pre>");
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                // Inside code block: only HTML-escape, no other processing
                result.AppendLine(EscapeHtml(line));
                continue;
            }

            // Handle blockquotes (> prefix)
            if (trimmed.StartsWith('>'))
            {
                var quoteContent = trimmed.Length > 1 ? trimmed[1..].TrimStart() : "";
                if (!inBlockquote)
                {
                    inBlockquote = true;
                }
                blockquoteLines.Add(quoteContent);
                continue;
            }

            // If we were in a blockquote and hit a non-quote line, flush it
            FlushBlockquote(result, blockquoteLines, ref inBlockquote);

            // Headers → bold text
            var headerMatch = HeaderRegex().Match(line);
            if (headerMatch.Success)
            {
                var headerText = headerMatch.Groups[2].Value;
                result.AppendLine($"<b>{ConvertInlineFormatting(headerText)}</b>");
                result.AppendLine();
                continue;
            }

            // Horizontal rules
            if (HorizontalRuleRegex().IsMatch(trimmed))
            {
                result.AppendLine("———");
                continue;
            }

            // Regular line — process inline formatting
            result.AppendLine(ConvertInlineFormatting(line));
        }

        // Flush any trailing blockquote
        FlushBlockquote(result, blockquoteLines, ref inBlockquote);

        // Close unclosed code block
        if (inCodeBlock)
        {
            result.AppendLine("</pre>");
        }

        return result.ToString().TrimEnd();
    }

    private static string ConvertInlineFormatting(string line)
    {
        // Split by inline code spans first — code content is only HTML-escaped
        var segments = SplitByInlineCode(line);
        var sb = new StringBuilder();

        foreach (var (text, isCode) in segments)
        {
            if (isCode)
            {
                sb.Append($"<code>{EscapeHtml(text)}</code>");
            }
            else
            {
                var processed = EscapeHtml(text);

                // Bold: **text** → <b>text</b>  (must come before italic)
                processed = BoldRegex().Replace(processed, "<b>$1</b>");

                // Italic: *text* → <i>text</i>
                processed = ItalicRegex().Replace(processed, "<i>$1</i>");

                // Strikethrough: ~~text~~ → <s>text</s>
                processed = StrikethroughRegex().Replace(processed, "<s>$1</s>");

                // Links: [text](url) — these were HTML-escaped, so fix the entities back
                processed = LinkRegex().Replace(processed, m =>
                {
                    var linkText = m.Groups[1].Value;
                    var url = m.Groups[2].Value
                        .Replace("&amp;", "&")
                        .Replace("&lt;", "<")
                        .Replace("&gt;", ">");
                    return $"<a href=\"{url}\">{linkText}</a>";
                });

                sb.Append(processed);
            }
        }

        return sb.ToString();
    }

    private static List<(string text, bool isCode)> SplitByInlineCode(string line)
    {
        var segments = new List<(string text, bool isCode)>();
        var inCode = false;
        var current = new StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '`')
            {
                // Save current segment
                if (current.Length > 0)
                {
                    segments.Add((current.ToString(), inCode));
                    current.Clear();
                }
                inCode = !inCode;
            }
            else
            {
                current.Append(line[i]);
            }
        }

        // Add remaining text
        if (current.Length > 0)
        {
            segments.Add((current.ToString(), inCode));
        }

        if (segments.Count == 0)
        {
            segments.Add((line, false));
        }

        return segments;
    }

    private static void FlushBlockquote(StringBuilder result, List<string> lines, ref bool inBlockquote)
    {
        if (!inBlockquote) return;

        var content = string.Join("\n", lines.Select(l => ConvertInlineFormatting(l)));
        result.AppendLine($"<blockquote>{content}</blockquote>");
        lines.Clear();
        inBlockquote = false;
    }

    public static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    // Regexes — source-generated for performance

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"^[-*_]{3,}\s*$")]
    private static partial Regex HorizontalRuleRegex();

    // Match **bold** but not escaped \*\*
    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    // Match *italic* but not **bold** (negative lookbehind/ahead for *)
    [GeneratedRegex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)")]
    private static partial Regex ItalicRegex();

    // Match ~~strikethrough~~
    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex StrikethroughRegex();

    // Match [text](url) — works on HTML-escaped text (parentheses aren't escaped)
    [GeneratedRegex(@"\[(.+?)\]\((.+?)\)")]
    private static partial Regex LinkRegex();
}
