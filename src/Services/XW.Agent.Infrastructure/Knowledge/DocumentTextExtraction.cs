using System.Text;
using System.Text.RegularExpressions;

namespace XW.Agent.Infrastructure.Knowledge;

/// <summary>按扩展名抽出纯文本。</summary>
public interface IDocumentTextExtractor
{
    /// <summary>是否支持该扩展名。</summary>
    bool CanExtract(string extension);

    /// <summary>从流里抽出全文。</summary>
    Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>纯文本：md、txt、csv、json 等。</summary>
public sealed class PlainTextDocumentExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".csv", ".json", ".log", ".yml", ".yaml", ".xml", ".html", ".htm"
    };

    /// <inheritdoc />
    public bool CanExtract(string extension) => Supported.Contains(Normalize(extension));

    /// <inheritdoc />
    public async Task<string> ExtractAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            content,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return text.Trim();
    }

    private static string Normalize(string extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
}

/// <summary>二进制格式暂不解析，避免静默入库空文本。</summary>
public sealed class UnsupportedBinaryDocumentExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> KnownBinary = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
    };

    /// <inheritdoc />
    public bool CanExtract(string extension) => KnownBinary.Contains(Normalize(extension));

    /// <inheritdoc />
    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName);
        throw new NotSupportedException(
            $"暂未内置 {ext} 解析器。请先转成 .md 或 .txt 再上传。");
    }

    private static string Normalize(string extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
}

/// <summary>按扩展名选择解析器。</summary>
public sealed class CompositeDocumentTextExtractor(IEnumerable<IDocumentTextExtractor> extractors) : IDocumentTextExtractor
{
    private readonly IDocumentTextExtractor[] _extractors = extractors.ToArray();

    /// <inheritdoc />
    public bool CanExtract(string extension) => _extractors.Any(extractor => extractor.CanExtract(extension));

    /// <inheritdoc />
    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName);
        var hit = _extractors.FirstOrDefault(extractor => extractor.CanExtract(ext));
        if (hit is null)
        {
            throw new NotSupportedException($"不支持的文件类型: {ext}。");
        }

        return hit.ExtractAsync(content, fileName, cancellationToken);
    }
}

/// <summary>按标题和段落切块。</summary>
public static class KnowledgeTextChunker
{
    private static readonly Regex ClausePattern = new(
        @"(?:^|\n)\s*(第[0-9０-９一二三四五六七八九十百零〇千]+条)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    /// <summary>把正文切成若干段。</summary>
    public static IReadOnlyList<string> Chunk(string text, int chunkSize = 700, int overlap = 80)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        chunkSize = Math.Max(200, chunkSize);
        overlap = Math.Clamp(overlap, 0, chunkSize / 2);

        var normalized = text.Replace("\r\n", "\n").Trim();
        var byHeading = Regex.Split(normalized, @"\n(?=#{1,3}\s)")
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToList();
        if (byHeading.Count == 0)
        {
            byHeading = [normalized];
        }

        var result = new List<string>();
        foreach (var section in byHeading)
        {
            if (section.Length <= chunkSize)
            {
                result.Add(section);
                continue;
            }

            var paras = Regex.Split(section, @"\n\s*\n")
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();
            if (paras.Count == 0)
            {
                paras = [section];
            }

            var buf = new StringBuilder();
            foreach (var para in paras)
            {
                if (buf.Length > 0 && buf.Length + para.Length + 2 > chunkSize)
                {
                    result.Add(buf.ToString());
                    var keep = overlap > 0 && buf.Length > overlap ? buf.ToString()[^overlap..] : string.Empty;
                    buf.Clear();
                    if (keep.Length > 0)
                    {
                        buf.Append(keep).Append("\n\n");
                    }
                }

                if (para.Length > chunkSize)
                {
                    if (buf.Length > 0)
                    {
                        result.Add(buf.ToString());
                        buf.Clear();
                    }

                    for (var i = 0; i < para.Length; i += chunkSize - overlap)
                    {
                        var len = Math.Min(chunkSize, para.Length - i);
                        result.Add(para.Substring(i, len));
                        if (i + len >= para.Length)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    if (buf.Length > 0)
                    {
                        buf.Append("\n\n");
                    }

                    buf.Append(para);
                }
            }

            if (buf.Length > 0)
            {
                result.Add(buf.ToString());
            }
        }

        return result.Where(part => part.Trim().Length >= 20).ToList();
    }

    /// <summary>有「第×条」时按条切开，否则按段落切。</summary>
    public static IReadOnlyList<KnowledgePiece> Split(string text, int chunkSize = 700, int overlap = 80)
    {
        var clauses = SplitClauses(text);
        if (clauses.Count < 2)
        {
            return Chunk(text, chunkSize, overlap)
                .Select(part => new KnowledgePiece(part, null))
                .ToArray();
        }

        var pieces = new List<KnowledgePiece>();
        foreach (var clause in clauses)
        {
            if (clause.Text.Length <= chunkSize)
            {
                pieces.Add(clause);
                continue;
            }

            foreach (var part in Chunk(clause.Text, chunkSize, overlap))
            {
                pieces.Add(new KnowledgePiece(part, clause.ClauseNo));
            }
        }

        return pieces;
    }

    private static IReadOnlyList<KnowledgePiece> SplitClauses(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Trim();
        var matches = ClausePattern.Matches(normalized);
        if (matches.Count < 2)
        {
            return [];
        }

        var clauses = new List<KnowledgePiece>();
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : normalized.Length;
            var body = normalized[start..end].Trim();
            if (body.Length < 20)
            {
                continue;
            }

            clauses.Add(new KnowledgePiece(body, matches[i].Groups[1].Value));
        }

        return clauses;
    }
}

/// <summary>一个切块。法规按条切开时带条款号。</summary>
public readonly record struct KnowledgePiece(string Text, string? ClauseNo);
