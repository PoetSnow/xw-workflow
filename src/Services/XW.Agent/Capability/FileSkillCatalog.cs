using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Options;
using XW.Agent.Application.Tooling;

namespace XW.Agent.Application.Capability;

/// <summary>
/// 从 skills/{domain}/*.json 加载 Skill。有文件时覆盖 appsettings 的 Capabilities。
/// 监视目录，保存后热加载，不必改引擎代码。
/// </summary>
public sealed class FileSkillCatalog : ICapabilityRegistry, ICapabilityCatalog, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly ILogger<FileSkillCatalog> _logger;
    private readonly object _gate = new();
    private readonly FileSystemWatcher? _watcher;
    private readonly string? _root;
    private CancellationTokenSource? _debounce;
    private IReadOnlyList<AgentCapabilityDefinition> _skills = [];

    public FileSkillCatalog(
        IOptionsMonitor<AgentOptions> options,
        IHostEnvironment env,
        ILogger<FileSkillCatalog> logger)
    {
        _options = options;
        _logger = logger;
        _root = ResolveRoot(options.CurrentValue.Skills, env);
        Reload("startup");

        if (options.CurrentValue.Skills is { Enabled: true, ReloadOnChange: true }
            && !string.IsNullOrWhiteSpace(_root)
            && Directory.Exists(_root))
        {
            _watcher = new FileSystemWatcher(_root)
            {
                Filter = "*.json",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                                | NotifyFilters.LastWrite
                                | NotifyFilters.CreationTime
                                | NotifyFilters.Size
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnChanged;
            _watcher.EnableRaisingEvents = true;
        }
    }

    public IReadOnlyList<AgentCapabilityDefinition> ListEnabled() =>
        Snapshot().Where(c => c.Enabled).ToList();

    public AgentCapabilityDefinition? Get(string code) =>
        Snapshot().FirstOrDefault(c =>
            c.Enabled && string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    public AgentCapabilityDefinition? FindByCode(string code) => Get(code);

    public AgentCapabilityDefinition? GetTechnicalTool(string code) => Get(code);

    public AgentCapabilityDefinition? FindByIntent(string intentCode)
    {
        var caps = ListEnabled();
        return caps.FirstOrDefault(c =>
                   string.Equals(c.IntentCode, intentCode, StringComparison.OrdinalIgnoreCase))
               ?? caps.FirstOrDefault(c =>
                   string.Equals(c.Code, intentCode, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<AgentCapabilityDefinition> ListByDomain(string domain) =>
        ListEnabled()
            .Where(c => string.Equals(c.Domain, domain, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<AgentCapabilityDefinition> ListByTag(string tag) =>
        ListEnabled()
            .Where(c => c.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    public IReadOnlyList<string> ListDomains() =>
        ListEnabled()
            .Select(c => c.Domain)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public void Dispose()
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        _watcher?.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        lock (_gate)
        {
            _debounce?.Cancel();
            _debounce = new CancellationTokenSource();
            var token = _debounce.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token);
                    Reload("watch:" + e.Name);
                }
                catch (OperationCanceledException)
                {
                    // 合并抖动
                }
            }, token);
        }
    }

    private void Reload(string reason)
    {
        var opt = _options.CurrentValue;
        List<AgentCapabilityDefinition> loaded;
        if (opt.Skills.Enabled && !string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
        {
            loaded = LoadFromDisk(_root, opt);
        }
        else
        {
            loaded = [];
        }

        if (loaded.Count == 0)
        {
            loaded = opt.Capabilities.Where(c => c.Enabled).Select(Clone).ToList();
            foreach (var c in loaded)
            {
                c.NormalizeContract();
            }

            _logger.LogInformation(
                "Skill catalog fallback to Agent:Capabilities count={Count} reason={Reason}",
                loaded.Count, reason);
        }
        else
        {
            _logger.LogInformation(
                "Skill catalog loaded from {Root} count={Count} reason={Reason} codes=[{Codes}]",
                _root, loaded.Count, reason,
                string.Join(",", loaded.Select(c => c.Code)));
        }

        lock (_gate)
        {
            _skills = loaded;
        }
    }

    private IReadOnlyList<AgentCapabilityDefinition> Snapshot()
    {
        lock (_gate)
        {
            return _skills;
        }
    }

    private List<AgentCapabilityDefinition> LoadFromDisk(string root, AgentOptions opt)
    {
        var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        var list = new List<AgentCapabilityDefinition>();
        var authCodes = opt.AuthProfiles.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var json = File.ReadAllText(file);
                var cap = JsonSerializer.Deserialize<AgentCapabilityDefinition>(json, JsonOptions);
                if (cap is null || string.IsNullOrWhiteSpace(cap.Code))
                {
                    _logger.LogWarning("Skip skill file without Code: {File}", file);
                    continue;
                }

                var folder = Path.GetFileName(Path.GetDirectoryName(file));
                if (string.IsNullOrWhiteSpace(cap.Domain) && !string.IsNullOrWhiteSpace(folder)
                    && !string.Equals(folder, "skills", StringComparison.OrdinalIgnoreCase))
                {
                    cap.Domain = folder!;
                }

                cap.NormalizeContract();

                if (cap.Http is not null)
                {
                    var profile = cap.Http.AuthProfile;
                    if (!string.IsNullOrWhiteSpace(profile) && !authCodes.Contains(profile))
                    {
                        cap.Http.AuthProfile = "none";
                    }

                    if (string.Equals(cap.Http.Adapter, "external", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(cap.Http.BaseUrl))
                    {
                        cap.Enabled = false;
                    }
                }

                list.Add(cap);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load skill file {File}", file);
            }
        }

        return list
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();
    }

    private static string? ResolveRoot(AgentSkillsOptions skills, IHostEnvironment env)
    {
        var path = string.IsNullOrWhiteSpace(skills.RootPath) ? "skills" : skills.RootPath.Trim();
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var underContent = Path.GetFullPath(Path.Combine(env.ContentRootPath, path));
        if (Directory.Exists(underContent))
        {
            return underContent;
        }

        var underBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        return Directory.Exists(underBase) ? underBase : underContent;
    }

    private static AgentCapabilityDefinition Clone(AgentCapabilityDefinition c)
    {
        var json = JsonSerializer.Serialize(c);
        return JsonSerializer.Deserialize<AgentCapabilityDefinition>(json, JsonOptions) ?? c;
    }
}
