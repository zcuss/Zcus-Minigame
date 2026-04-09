using System.Net;
using System.Text;
using System.Text.Json;

namespace AutoMinigameWinForms.Core;

public sealed class LogBridgeServer : IDisposable
{
    private const int MaxStoredLines = 4000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly object _sync = new();
    private readonly List<LogLine> _appLines = [];
    private readonly List<LogLine> _testerLines = [];
    private Task? _loopTask;
    private bool _started;

    public string BaseUrl { get; }

    public LogBridgeServer(string baseUrl)
    {
        BaseUrl = NormalizeBaseUrl(baseUrl);
        _listener.Prefixes.Add(BaseUrl);
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        try
        {
            _listener.Start();
            _loopTask = Task.Run(() => RunAsync(_cts.Token));
            _started = true;
        }
        catch
        {
            _started = false;
        }
    }

    public void PublishAppLog(string line)
    {
        AddLine(_appLines, line, "app");
    }

    public void Clear()
    {
        lock (_sync)
        {
            _appLines.Clear();
            _testerLines.Clear();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
        }
        catch
        {
            // Ignore stop errors on shutdown.
        }

        try
        {
            _listener.Close();
        }
        catch
        {
            // Ignore close errors on shutdown.
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch
            {
                continue;
            }

            if (ctx is null)
            {
                continue;
            }

            _ = Task.Run(() => HandleContextAsync(ctx), token);
        }
    }

    private async Task HandleContextAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var resp = ctx.Response;
        try
        {
            WriteCorsHeaders(resp);

            if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                resp.StatusCode = 204;
                return;
            }

            var path = NormalizePath(req.Url?.AbsolutePath);
            if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && path == "/api/ping")
            {
                await WriteJsonAsync(resp, new { ok = true, api = "log-bridge", baseUrl = BaseUrl });
                return;
            }

            if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && path == "/api/logs/app")
            {
                var payload = await ReadJsonAsync<IngestRequest>(req);
                AddLine(_appLines, payload?.Line, payload?.Source ?? "app");
                await WriteJsonAsync(resp, new { ok = true });
                return;
            }

            if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && path == "/api/logs/tester")
            {
                var payload = await ReadJsonAsync<IngestRequest>(req);
                AddLine(_testerLines, payload?.Line, payload?.Source ?? "tester");
                await WriteJsonAsync(resp, new { ok = true });
                return;
            }

            if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && path == "/api/logs/clear")
            {
                Clear();
                await WriteJsonAsync(resp, new { ok = true });
                return;
            }

            if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && path == "/api/logs/compare")
            {
                var result = BuildCompare();
                await WriteJsonAsync(resp, result);
                return;
            }

            resp.StatusCode = 404;
            await WriteJsonAsync(resp, new { ok = false, error = "not-found" }, 404);
        }
        catch
        {
            if (!resp.OutputStream.CanWrite)
            {
                return;
            }

            await WriteJsonAsync(resp, new { ok = false, error = "internal-error" }, 500);
        }
        finally
        {
            try
            {
                resp.Close();
            }
            catch
            {
                // Ignore close errors for short-lived HTTP clients.
            }
        }
    }

    private CompareResult BuildCompare()
    {
        List<LogLine> app;
        List<LogLine> tester;
        lock (_sync)
        {
            app = [.. _appLines];
            tester = [.. _testerLines];
        }

        var appClicks = app.Count(x => x.Line.Contains("CLICK ", StringComparison.OrdinalIgnoreCase));
        var appSessionRows = app.Count(x => x.Line.StartsWith("Session |", StringComparison.OrdinalIgnoreCase));

        var testerAttempts = tester.Count(x => x.Line.Contains(" req=", StringComparison.OrdinalIgnoreCase) && x.Line.Contains(" press=", StringComparison.OrdinalIgnoreCase));
        var testerHits = tester.Count(x => x.Line.Contains(" HIT ", StringComparison.OrdinalIgnoreCase));
        var testerMisses = tester.Count(x => x.Line.Contains(" MISS ", StringComparison.OrdinalIgnoreCase));
        var testerTimeouts = tester.Count(x => x.Line.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase));

        return new CompareResult(
            AppCount: app.Count,
            TesterCount: tester.Count,
            AppClicks: appClicks,
            AppSessionRows: appSessionRows,
            TesterAttempts: testerAttempts,
            TesterHits: testerHits,
            TesterMisses: testerMisses,
            TesterTimeouts: testerTimeouts,
            ClickGap: appClicks - testerAttempts,
            LastApp: app.LastOrDefault()?.Line ?? string.Empty,
            LastTester: tester.LastOrDefault()?.Line ?? string.Empty);
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpListenerRequest req) where T : class
    {
        if (req.InputStream is null || !req.HasEntityBody)
        {
            return null;
        }

        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
        var raw = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(raw, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse resp, object payload, int statusCode = 200)
    {
        resp.StatusCode = statusCode;
        resp.ContentType = "application/json; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        resp.ContentLength64 = bytes.LongLength;
        await resp.OutputStream.WriteAsync(bytes);
    }

    private static void WriteCorsHeaders(HttpListenerResponse resp)
    {
        resp.Headers["Access-Control-Allow-Origin"] = "*";
        resp.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
        resp.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }

    private void AddLine(List<LogLine> target, string? line, string source)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_sync)
        {
            target.Add(new LogLine(DateTimeOffset.UtcNow, source, line.Trim()));
            if (target.Count > MaxStoredLines)
            {
                target.RemoveRange(0, target.Count - MaxStoredLines);
            }
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var p = path.Trim();
        if (!p.StartsWith('/'))
        {
            p = "/" + p;
        }

        if (p.Length > 1 && p.EndsWith('/'))
        {
            p = p[..^1];
        }

        return p.ToLowerInvariant();
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl) ? AppConstants.LogBridgeApiBaseUrl : baseUrl.Trim();
        if (!url.EndsWith('/'))
        {
            url += "/";
        }

        return url;
    }

    private sealed record LogLine(DateTimeOffset TsUtc, string Source, string Line);
    private sealed record IngestRequest(string? Source, string? Line);
    private sealed record CompareResult(
        int AppCount,
        int TesterCount,
        int AppClicks,
        int AppSessionRows,
        int TesterAttempts,
        int TesterHits,
        int TesterMisses,
        int TesterTimeouts,
        int ClickGap,
        string LastApp,
        string LastTester);
}
