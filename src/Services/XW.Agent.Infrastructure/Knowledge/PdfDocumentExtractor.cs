using UglyToad.PdfPig;

namespace XW.Agent.Infrastructure.Knowledge;

/// <summary>从 PDF 抽出文字。扫描件没有文字层时拒绝入库。</summary>
public sealed class PdfDocumentExtractor : IDocumentTextExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string extension) =>
        string.Equals(Normalize(extension), ".pdf", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        buffer.Position = 0;
        using var document = PdfDocument.Open(buffer);
        var text = new System.Text.StringBuilder();
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            text.AppendLine(page.Text);
        }

        var result = text.ToString().Trim();
        if (result.Length == 0)
        {
            throw new InvalidOperationException("PDF 未抽出文字。扫描件需要先做文字识别。");
        }

        return Task.FromResult(result);
    }

    private static string Normalize(string extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
}
