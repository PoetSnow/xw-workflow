using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Context;
using XW.Agent.Application.Knowledge;
using XW.Agent.Application.Options;
using XW.Agent.Application.Runtime;

namespace XW.Agent.Application.Controllers;

/// <summary>知识库：上传入库 + 向量检索调试。</summary>
[ApiController]
[Route("api/agent/knowledge")]
public sealed class AgentKnowledgeController(
    IKnowledgeRetriever retriever,
    IKnowledgeIngestService ingest,
    IKnowledgeVectorStore vectorStore,
    IKnowledgeStore lexicalStore,
    IOptionsMonitor<AgentOptions> options) : ControllerBase
{
    /// <summary>知识库状态：开关、Embedding、文档/切块计数。</summary>
    [HttpGet]
    public async Task<ActionResult<object>> Status(CancellationToken ct)
    {
        var cfg = options.CurrentValue.Knowledge;
        var provider = cfg.Provider?.Trim() ?? "vector";
        object counts;
        IReadOnlyList<KnowledgeDocumentRecord>? docs = null;

        if (string.Equals(provider, "vector", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(cfg.ConnectionString))
        {
            try
            {
                var (documents, chunks) = await vectorStore.CountAsync(ct);
                docs = await vectorStore.ListDocumentsAsync(20, ct);
                counts = new { documents, chunks };
            }
            catch (Exception ex)
            {
                counts = new { documents = 0, chunks = 0, error = ex.Message };
            }
        }
        else
        {
            counts = new { documents = lexicalStore.DocumentCount, chunks = lexicalStore.ChunkCount };
        }

        return Ok(new
        {
            enabled = cfg.Enabled,
            provider,
            topK = cfg.TopK,
            minSimilarity = cfg.MinSimilarity,
            embedding = new
            {
                cfg.Embedding.Enabled,
                endpoint = cfg.Embedding.Endpoint,
                model = cfg.Embedding.ModelName,
                dimensions = cfg.Embedding.Dimensions
            },
            hasConnectionString = !string.IsNullOrWhiteSpace(cfg.ConnectionString),
            counts,
            documents = docs
        });
    }

    /// <summary>调试检索：按问句返回命中片段。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<object>> Search(
        [FromQuery] string q,
        [FromQuery] int topK = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "q is required" });
        }

        var k = topK > 0 ? topK : options.CurrentValue.Knowledge.TopK;
        var hits = await retriever.SearchAsync(q, new UserContext(), k, ct);
        return Ok(new
        {
            query = q,
            provider = options.CurrentValue.Knowledge.Provider,
            count = hits.Count,
            hits
        });
    }

    /// <summary>HTTP 上传不规则文件 → 解析 → 切块 → Embedding → pgvector。</summary>
    /// <remarks>
    /// 必须 <c>multipart/form-data</c>，文件字段名优先 <c>file</c>（也接受任意单文件字段）。
    /// curl 示例：<c>curl -F "file=@说明.md" -F "domain=aftersales" http://host/api/agent/knowledge/upload</c>
    /// </remarks>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<object>> Upload(
        [FromForm] KnowledgeUploadForm form,
        CancellationToken ct = default)
    {
        // Swagger/Swashbuckle 要求 IFormFile 放在 Form 模型里，不要直接 [FromForm] IFormFile
        var file = form.File
                   ?? Request.Form.Files.GetFile("file")
                   ?? Request.Form.Files.GetFile("File")
                   ?? Request.Form.Files.FirstOrDefault();

        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                message =
                    "未收到上传文件。请使用 multipart/form-data，表单字段名为 file。" +
                    "示例：curl -F \"file=@文档.md\" http://localhost:5010/api/agent/knowledge/upload。" +
                    "纯文本可用 POST /api/agent/knowledge/ingest-text。",
                contentType = Request.ContentType,
                formFileCount = Request.HasFormContentType ? Request.Form.Files.Count : 0
            });
        }

        await using var stream = file.OpenReadStream();
        using var deadline = RequestDeadline.Start(ct, options.CurrentValue.Knowledge.UploadDeadlineSeconds);
        try
        {
            var result = await ingest.IngestAsync(new KnowledgeIngestRequest
            {
                Content = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Domain = form.Domain,
                Title = form.Title,
                DocumentId = form.DocumentId
            }, deadline.Token);

            return Ok(result);
        }
        catch (OperationCanceledException) when (deadline.Fired)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, new
            {
                message = "上传超过时间限制，已停止。",
                phase = "deadline"
            });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>直接提交纯文本（联调/无文件场景）。</summary>
    [HttpPost("ingest-text")]
    public async Task<ActionResult<object>> IngestText(
        [FromBody] KnowledgeIngestTextBody body,
        CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Text))
        {
            return BadRequest(new { message = "text is required" });
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(body.Text);
        await using var ms = new MemoryStream(bytes);
        var name = string.IsNullOrWhiteSpace(body.FileName) ? "paste.md" : body.FileName!;

        try
        {
            var result = await ingest.IngestAsync(new KnowledgeIngestRequest
            {
                Content = ms,
                FileName = name,
                ContentType = "text/plain",
                Domain = body.Domain,
                Title = body.Title,
                DocumentId = body.DocumentId
            }, ct);
            return Ok(result);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>按文档 Id 删除向量库中的文档与切块。</summary>
    [HttpDelete("{documentId}")]
    public async Task<ActionResult> Delete(string documentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return BadRequest();
        }

        await ingest.DeleteAsync(documentId, ct);
        return NoContent();
    }
}

/// <summary>知识文件上传表单（multipart）；供 Swagger 正确生成 file 控件。</summary>
public sealed class KnowledgeUploadForm
{
    /// <summary>上传文件，表单字段名 file。</summary>
    public IFormFile? File { get; set; }

    /// <summary>业务域（可选）。</summary>
    public string? Domain { get; set; }

    /// <summary>标题（可选）。</summary>
    public string? Title { get; set; }

    /// <summary>指定文档 Id（可选）。</summary>
    public string? DocumentId { get; set; }
}

/// <summary>纯文本入库请求体（ingest-text）。</summary>
public sealed class KnowledgeIngestTextBody
{
    /// <summary>正文。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>虚拟文件名（影响解析扩展名，默认 paste.md）。</summary>
    public string? FileName { get; set; }

    /// <summary>业务域。</summary>
    public string? Domain { get; set; }

    /// <summary>标题。</summary>
    public string? Title { get; set; }

    /// <summary>指定文档 Id。</summary>
    public string? DocumentId { get; set; }
}
