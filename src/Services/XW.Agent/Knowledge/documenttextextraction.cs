using System.Text;
using System.Text.RegularExpressions;

namespace XW.Agent.Application.Knowledge;

/// <summary>按文件扩展名抽取纯文本（入库前解析）。</summary>
public interface IDocumentTextExtractor
{
    /// <summary>是否支持该扩展名（含点，小写）。</summary>
    bool CanExtract(string extension);

    /// <summary>从流中提取全文。</summary>
    Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default);
}

/// <summary>纯文本类：md/txt/csv/json/log 等不规则源文件的第一档支持。</summary>
public sealed class PlainTextDocumentExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".csv", ".json", ".log", ".yml", ".yaml", ".xml", ".html", ".htm"
    };

    public bool CanExtract(string extension) => Supported.Contains(Normalize(extension));

    public async Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return text.Trim();
    }

    private static string Normalize(string extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
}

/// <summary>
/// PDF/Word 等二进制格式占位：抛出明确错误，后续可接 PdfPig / OpenXml。
/// 生产应替换为真实解析器，而不是词库。
/// </summary>
public sealed class UnsupportedBinaryDocumentExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> KnownBinary = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
    };

    public bool CanExtract(string extension) => KnownBinary.Contains(Normalize(extension));

    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName);
        throw new NotSupportedException(
            $"暂未内置 {ext} 解析器。骨架已预留 IDocumentTextExtractor；" +
            "请接入 PdfPig / DocumentFormat.OpenXml 等，解析为纯文本后再切块向量化。" +
            "当前可先转成 .md/.txt 上传。");
    }

    private static string Normalize(string extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
}

/// <summary>组合多个解析器：按扩展名委派；都不支持则抛错。</summary>
public sealed class CompositeDocumentTextExtractor(IEnumerable<IDocumentTextExtractor> extractors) : IDocumentTextExtractor
{
    private readonly IDocumentTextExtractor[] _extractors = extractors.ToArray();

    /// <inheritdoc />
    public bool CanExtract(string extension) =>
        _extractors.Any(e => e.CanExtract(extension));

    /// <inheritdoc />
    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName);
        var hit = _extractors.FirstOrDefault(e => e.CanExtract(ext));
        if (hit is null)
        {
            throw new NotSupportedException(
                $"不支持的文件类型: {ext}。当前可上传: md/txt/csv/json/log/yml/xml/html；" +
                "pdf/docx 需接入对应 IDocumentTextExtractor。");
        }

        return hit.ExtractAsync(content, fileName, ct);
    }
}

/// <summary>按标题/段落切块，面向不规则长文，不维护词库。</summary>
public static class KnowledgeTextChunker
{
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
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
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
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
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
                    var keep = overlap > 0 && buf.Length > overlap
                        ? buf.ToString()[^overlap..]
                        : string.Empty;
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

        return result.Where(x => x.Trim().Length >= 20).ToList();
    }
}
