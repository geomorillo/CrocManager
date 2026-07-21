// CrocManager - Interfaz gráfica para croc
// Autor: Manuel Jhobanny Morillo Ordoñez
// © 2026 - Todos los derechos reservados

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebDesktop.Core;
using CrocManager.Models;

namespace CrocManager.Services;

public partial class CrocService
{
    private Process? _activeProcess;
    private readonly object _lock = new();
    private WebWindow? _window;
    private readonly HistoryService _history = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public void SetWindow(WebWindow window) => _window = window;

    // ─── Check install ───────────────────────────────────

    public Task<string> CheckInstall(string _) => Task.Run(() =>
    {
        try
        {
            var (exit, stdout, stderr) = RunCroc("--version");
            var ok = exit == 0 && !string.IsNullOrEmpty(stdout);
            return JsonSerializer.Serialize(new
            {
                success = ok,
                version = ok ? stdout.Trim() : null,
                error = ok ? null : stderr
            }, JsonOpts);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    });

    // ─── Send files ──────────────────────────────────────

    public Task<string> SendFiles(string json)
    {
        var tcs = new TaskCompletionSource<string>();

        Task.Run(async () =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var paths = root.TryGetProperty("paths", out var p)
                    ? p.EnumerateArray().Select(x => x.GetString()!).Where(x => x != null).ToList()
                    : new List<string>();
                var userCode = root.TryGetProperty("code", out var c) ? c.GetString() : null;

                if (paths.Count == 0)
                {
                    tcs.TrySetResult(Error("No files selected"));
                    return;
                }

                var cmdArgs = new List<string> { "send" };
                if (!string.IsNullOrWhiteSpace(userCode))
                {
                    if (userCode.Length < 6)
                    {
                        tcs.TrySetResult(Error("Code must be at least 6 characters"));
                        return;
                    }
                    cmdArgs.Add("--code");
                    cmdArgs.Add(userCode);
                }
                cmdArgs.AddRange(paths.Select(p => $"\"{p}\""));

                var psi = new ProcessStartInfo
                {
                    FileName = "croc",
                    Arguments = string.Join(" ", cmdArgs),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var proc = new Process { StartInfo = psi };
                lock (_lock) _activeProcess = proc;

                var stderrBuilder = new System.Text.StringBuilder();
                var codeExtracted = false;
                var extractedCode = "";
                var codeSignal = new ManualResetEventSlim(false);

                proc.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is null) return;
                    stderrBuilder.AppendLine(e.Data);

                    if (!codeExtracted)
                    {
                        var m = CodeRegex().Match(e.Data);
                        if (m.Success)
                        {
                            extractedCode = m.Groups[1].Value;
                            codeExtracted = true;
                            codeSignal.Set();
                            PushToJS("onCodeReady", JsonSerializer.Serialize(new { code = extractedCode }, JsonOpts));
                        }
                    }

                    var pMatch = ProgressRegex().Match(e.Data);
                    if (pMatch.Success)
                    {
                        var percent = pMatch.Groups[1].Value;
                        var extra = pMatch.Groups[2].Success ? pMatch.Groups[2].Value.Trim() : "";
                        PushToJS("onProgress", JsonSerializer.Serialize(new
                        {
                            percent = int.Parse(percent),
                            extra,
                            phase = "sending"
                        }, JsonOpts));
                    }

                    // Detectar fin de transferencia
                    if (e.Data.Contains("Sent", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("transferred", StringComparison.OrdinalIgnoreCase))
                    {
                        PushToJS("onTransferComplete", "{}");
                    }
                };

                proc.Start();
                proc.BeginErrorReadLine();

                // Esperar a que aparezca el código (máx 15 segundos)
                if (!codeSignal.Wait(15000))
                {
                    // Si no aparece en 15s, revisar stdout
                    var stdout = proc.StandardOutput.ReadToEnd();
                    var m = CodeRegex().Match(stdout);
                    if (m.Success)
                    {
                        extractedCode = m.Groups[1].Value;
                        codeExtracted = true;
                    }
                }

                // Devolver el código al frontend inmediatamente
                if (!string.IsNullOrEmpty(extractedCode))
                {
                    tcs.TrySetResult(JsonSerializer.Serialize(new
                    {
                        success = true,
                        code = extractedCode
                    }, JsonOpts));
                }
                else
                {
                    // No se encontró código, esperar a que termine
                    proc.WaitForExit(10000);
                    lock (_lock) _activeProcess = null;
                    var stderr = stderrBuilder.ToString();
                    tcs.TrySetResult(Error(stderr));
                    return;
                }

                // ─── Seguimiento en segundo plano ───
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Leer stdout restante
                        var stdout = await proc.StandardOutput.ReadToEndAsync();
                        proc.WaitForExit();
                        lock (_lock) _activeProcess = null;

                        var stderr = stderrBuilder.ToString();
                        var status = proc.ExitCode == 0 ? "completed" : "error";

                        _history.Add(new HistoryEntry
                        {
                            Type = "send",
                            Status = status,
                            Code = extractedCode,
                            Files = paths,
                            Error = proc.ExitCode != 0 ? stderr : null,
                            SizeBytes = 0
                        });

                        proc.Dispose();
                    }
                    catch { /* proceso ya terminó */ }
                });
            }
            catch (Exception ex)
            {
                _history.Add(new HistoryEntry { Type = "send", Status = "error", Error = ex.Message });
                tcs.TrySetResult(Error(ex.Message));
            }
        });

        return tcs.Task;
    }

    // ─── Send text ───────────────────────────────────────

    public Task<string> SendText(string json)
    {
        return Task.Run(async () =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var text = root.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;

                if (string.IsNullOrWhiteSpace(text))
                    return Error("No text to send");

                var cmdArgs = new List<string> { "send", "--text", $"\"{text}\"" };
                if (!string.IsNullOrWhiteSpace(code))
                {
                    if (code.Length < 6)
                        return Error("Code must be at least 6 characters");
                    cmdArgs.Add("--code");
                    cmdArgs.Add(code);
                }

                var (exit, stdout, stderr) = RunCroc(string.Join(" ", cmdArgs));

                var match = CodeRegex().Match(stdout);
                var extractedCode = match.Success ? match.Groups[1].Value : stdout.Trim();

                _history.Add(new HistoryEntry
                {
                    Type = "text",
                    Status = exit == 0 ? "completed" : "error",
                    Code = extractedCode,
                    TextContent = text,
                    Error = exit != 0 ? stderr : null
                });

                if (exit != 0)
                    return Error(stderr);

                return JsonSerializer.Serialize(new { success = true, code = extractedCode }, JsonOpts);
            }
            catch (Exception ex)
            {
                _history.Add(new HistoryEntry { Type = "text", Status = "error", Error = ex.Message });
                return Error(ex.Message);
            }
        });
    }

    // ─── Receive ─────────────────────────────────────────

    public Task<string> Receive(string json)
    {
        return Task.Run(async () =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var code = root.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
                var destination = root.TryGetProperty("destination", out var d) ? d.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(code))
                    return Error("No code provided");
                if (string.IsNullOrWhiteSpace(destination))
                    return Error("No destination folder selected");

                if (!Directory.Exists(destination))
                    Directory.CreateDirectory(destination);

                var psi = new ProcessStartInfo
                {
                    FileName = "croc",
                    Arguments = $"--yes --internal-dns \"{code}\"",
                    WorkingDirectory = destination,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi };
                lock (_lock) _activeProcess = proc;

                var stderrBuilder = new System.Text.StringBuilder();

                proc.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is null) return;
                    stderrBuilder.AppendLine(e.Data);

                    var pMatch = ProgressRegex().Match(e.Data);
                    if (pMatch.Success)
                    {
                        PushToJS("onProgress", JsonSerializer.Serialize(new
                        {
                            percent = int.Parse(pMatch.Groups[1].Value),
                            extra = pMatch.Groups[2].Success ? pMatch.Groups[2].Value.Trim() : "",
                            phase = "receiving"
                        }, JsonOpts));
                    }
                };

                proc.Start();
                proc.BeginErrorReadLine();
                var stdout = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(30000);

                lock (_lock) _activeProcess = null;
                var stderr = stderrBuilder.ToString();
                var status = proc.ExitCode == 0 ? "completed" : "error";

                _history.Add(new HistoryEntry
                {
                    Type = "receive",
                    Status = status,
                    Code = code,
                    Destination = destination,
                    Error = proc.ExitCode != 0 ? stderr : null
                });

                if (proc.ExitCode != 0)
                    return Error(stderr);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Files received successfully",
                    destination
                }, JsonOpts);
            }
            catch (Exception ex)
            {
                _history.Add(new HistoryEntry { Type = "receive", Status = "error", Error = ex.Message });
                return Error(ex.Message);
            }
        });
    }

    // ─── History ─────────────────────────────────────────

    public Task<string> GetHistory(string _) => Task.Run(() =>
    {
        var entries = _history.GetAll().Select(e => new
        {
            e.Id,
            e.Timestamp,
            e.Type,
            e.Status,
            e.Code,
            e.Destination,
            e.Files,
            e.TextContent,
            e.Error,
            SizeBytes = e.SizeBytes,
            TimeAgo = GetTimeAgo(e.Timestamp)
        }).ToList();

        return JsonSerializer.Serialize(new { success = true, entries }, JsonOpts);
    });

    public Task<string> ClearHistory(string _) => Task.Run(() =>
    {
        _history.Clear();
        return Ok("History cleared");
    });

    // ─── Cancel ──────────────────────────────────────────

    public Task<string> CancelTransfer(string _) => Task.Run(() =>
    {
        lock (_lock)
        {
            if (_activeProcess is not null && !_activeProcess.HasExited)
            {
                _activeProcess.Kill(entireProcessTree: true);
                _activeProcess.Dispose();
                _activeProcess = null;
                return Ok("Transfer cancelled");
            }
            return Ok("No active transfer");
        }
    });

    // ─── Helpers ─────────────────────────────────────────

    private void PushToJS(string fn, string data)
    {
        if (_window is null) return;
        try
        {
            var js = $"window.{fn}({data});";
            if (_window.InvokeRequired)
                _window.BeginInvoke(() => _ = _window.ExecuteScriptAsync(js));
            else
                _ = _window.ExecuteScriptAsync(js);
        }
        catch { /* ignore if window closed */ }
    }

    private static (int ExitCode, string Stdout, string Stderr) RunCroc(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "croc",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(10000);
        return (proc.ExitCode, stdout, stderr);
    }

    private static string Ok(object? data = null) =>
        JsonSerializer.Serialize(new { success = true, error = (string?)null, data }, JsonOpts);

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { success = false, error = message }, JsonOpts);

    private static string GetTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("MMM dd");
    }

    [GeneratedRegex(@"Code is:\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^\s*(\d+)%.*\(([^)]*)\)")]
    private static partial Regex ProgressRegex();
}
