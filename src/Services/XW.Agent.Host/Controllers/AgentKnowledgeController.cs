using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using XW.Agent.Contracts.Knowledge;

namespace XW.Agent.Host.Controllers;

/// <summary>知识库调试：检索、入库、删除。检索直接调用检索接口。</summary>
[ApiController]
[Route("api/agent/knowledge")]
public sealed class AgentKnowledgeController(
    IKnowledgeRetriever retriever,
    IKnowledgeIngest ingest,
    IOptionsMonitor<KnowledgeConnectionOptions> knowledge,
    IOptionsMonitor<EmbeddingOptions> embedding) : ControllerBase
{
    /// <summary>知识库开关和 Embedding 配置。</summary>
    [HttpGet]
    public ActionResult<object> Status()
    {
        var cfg = knowledge.CurrentValue;
        var embed = embedding.CurrentValue;
        return Ok(new
        {
            enabled = cfg.Enabled,
            provider = cfg.Provider,
            topK = cfg.TopK,
            minSimilarity = cfg.MinSimilarity,
            hasConnectionString = !string.IsNullOrWhiteSpace(cfg.ConnectionString),
            embedding = new
            {
                embed.Enabled,
                endpoint = embed.Endpoint,
                model = embed.ModelName,
                dimensions = embed.Dimensions
            }
        });
    }

    /// <summary>按资料集检索。缺少资料集返回 400，不扫全库。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<object>> Search(
        [FromQuery] string q,
        [FromQuery] string[]? corpus,
        [FromQuery] int topK = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "q is required" });
        }

        var corpusIds = ParseCorpus(corpus);
        if (corpusIds.Count == 0)
        {
            return BadRequest(new { message = "corpus is required" });
        }

        var result = await retriever.SearchAsync(new KnowledgeSearchRequest
        {
            Query = q,
            CorpusIds = corpusIds,
            TopK = topK
        }, cancellationToken);

        if (result.Rejected)
        {
            return BadRequest(new { message = result.Reason ?? "缺少资料集编号。" });
        }

        return Ok(new
        {
            query = q,
            corpus = corpusIds,
            count = result.Hits.Count,
            hits = result.Hits
        });
    }

    /// <summary>上传文件并入库到指定资料集。</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<object>> Upload(
        [FromForm] KnowledgeUploadForm form,
        CancellationToken cancellationToken = default)
    {
        var file = form.File
                   ?? Request.Form.Files.GetFile("file")
                   ?? Request.Form.Files.GetFile("File")
                   ?? Request.Form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "未收到上传文件。请使用 multipart/form-data，字段名为 file。" });
        }

        var corpusId = FirstCorpus(form.Corpus, form.Domain);
        if (corpusId.Length == 0)
        {
            return BadRequest(new { message = "corpus is required" });
        }

        await using var stream = file.OpenReadStream();
        using var deadline = LinkDeadline(cancellationToken, knowledge.CurrentValue.UploadDeadlineSeconds);
        try
        {
            var result = await ingest.IngestAsync(new KnowledgeIngestRequest
            {
                Content = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                CorpusId = corpusId,
                Title = form.Title,
                DocumentId = form.DocumentId
            }, deadline.Token);
            return Ok(result);
        }
        catch (OperationCanceledException) when (deadline.Fired)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, new { message = "上传超过时间限制，已停止。" });
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>提交纯文本并入库到指定资料集。</summary>
    [HttpPost("ingest-text")]
    public async Task<ActionResult<object>> IngestText(
        [FromBody] KnowledgeIngestTextBody body,
        CancellationToken cancellationToken = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Text))
        {
            return BadRequest(new { message = "text is required" });
        }

        var corpusId = FirstCorpus(body.Corpus, body.Domain);
        if (corpusId.Length == 0)
        {
            return BadRequest(new { message = "corpus is required" });
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(body.Text);
        await using var stream = new MemoryStream(bytes);
        var name = string.IsNullOrWhiteSpace(body.FileName) ? "paste.md" : body.FileName;
        try
        {
            var result = await ingest.IngestAsync(new KnowledgeIngestRequest
            {
                Content = stream,
                FileName = name,
                ContentType = "text/plain",
                CorpusId = corpusId,
                Title = body.Title,
                DocumentId = body.DocumentId
            }, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>按资料集和文档 Id 删除。</summary>
    [HttpDelete("{documentId}")]
    public async Task<ActionResult> Delete(
        string documentId,
        [FromQuery] string? corpus,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return BadRequest(new { message = "documentId is required" });
        }

        if (string.IsNullOrWhiteSpace(corpus))
        {
            return BadRequest(new { message = "corpus is required" });
        }

        try
        {
            await ingest.DeleteAsync(corpus, documentId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private UploadDeadline LinkDeadline(CancellationToken caller, int seconds)
    {
        if (seconds <= 0)
        {
            return new UploadDeadline(caller, null);
        }

        var owned = CancellationTokenSource.CreateLinkedTokenSource(caller);
        owned.CancelAfter(TimeSpan.FromSeconds(seconds));
        return new UploadDeadline(caller, owned);
    }

    private static List<string> ParseCorpus(string[]? corpus)
    {
        if (corpus is null || corpus.Length == 0)
        {
            return [];
        }

        return corpus
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string FirstCorpus(string? corpus, string? domain)
    {
        if (!string.IsNullOrWhiteSpace(corpus))
        {
            return corpus.Trim();
        }

        return string.IsNullOrWhiteSpace(domain) ? string.Empty : domain.Trim();
    }

    private sealed class UploadDeadline : IDisposable
    {
        private readonly CancellationToken _caller;
        private readonly CancellationTokenSource? _owned;

        public UploadDeadline(CancellationToken caller, CancellationTokenSource? owned)
        {
            _caller = caller;
            _owned = owned;
            Token = owned?.Token ?? caller;
        }

        public CancellationToken Token { get; }

        public bool Fired =>
            _owned is not null && _owned.IsCancellationRequested && !_caller.IsCancellationRequested;

        public void Dispose() => _owned?.Dispose();
    }
}

/// <summary>知识文件上传表单。</summary>
public sealed class KnowledgeUploadForm
{
    /// <summary>上传文件，字段名 file。</summary>
    public IFormFile? File { get; set; }

    /// <summary>资料集编号。</summary>
    public string? Corpus { get; set; }

    /// <summary>资料集编号。与 corpus 二选一。</summary>
    public string? Domain { get; set; }

    /// <summary>标题。</summary>
    public string? Title { get; set; }

    /// <summary>指定文档 Id。</summary>
    public string? DocumentId { get; set; }
}

/// <summary>纯文本入库请求体。</summary>
public sealed class KnowledgeIngestTextBody
{
    /// <summary>正文。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>虚拟文件名。</summary>
    public string? FileName { get; set; }

    /// <summary>资料集编号。</summary>
    public string? Corpus { get; set; }

    /// <summary>资料集编号。与 corpus 二选一。</summary>
    public string? Domain { get; set; }

    /// <summary>标题。</summary>
    public string? Title { get; set; }

    /// <summary>指定文档 Id。</summary>
    public string? DocumentId { get; set; }
}
