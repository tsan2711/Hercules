using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class StockfishEngine : IDisposable
{
    public bool IsReady => _proc is { HasExited: false };

    Process _proc;
    readonly ConcurrentQueue<string> _out = new();
    CancellationTokenSource _cts;

    // ???ng d?n engine: Assets/StreamingAssets/Engines/Windows/stockfish.exe
    string EnginePath() =>
        Path.Combine(Application.streamingAssetsPath, "Engines", "Windows", "stockfish.exe");

    public async Task StartAsync(
        int hashMB = 128,
        int threads = 2,
        int? elo = null,              // null => dùng Skill Level
        int skill = 20,               // 0..20
        int contempt = 0              // -100..100
    )
    {
        if (IsReady) return;

        // T?o process
        _cts = new CancellationTokenSource();
        _proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = EnginePath(),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };
        _proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _out.Enqueue(e.Data); };

        if (!_proc.Start()) throw new Exception("Cannot start Stockfish process.");
        _proc.BeginOutputReadLine();

        // Handshake UCI
        await SendAsync("uci");
        await WaitForAsync("uciok", 5000);

        // Options c? b?n
        await SendAsync($"setoption name Hash value {hashMB}");
        await SendAsync($"setoption name Threads value {threads}");
        await SendAsync($"setoption name Contempt value {contempt}");

        if (elo.HasValue)
        {
            await SendAsync("setoption name UCI_LimitStrength value true");
            await SendAsync($"setoption name UCI_Elo value {Mathf.Clamp(elo.Value, 1350, 2850)}");
        }
        else
        {
            await SendAsync("setoption name UCI_LimitStrength value false");
            await SendAsync($"setoption name Skill Level value {Mathf.Clamp(skill, 0, 20)}");
        }

        await SendAsync("isready");
        await WaitForAsync("readyok", 5000);
    }

    /// <summary>
    /// Yêu c?u Stockfish tr? "bestmove" t? FEN (và optional history UCI). 
    /// N?u depth != null s? ?u tiên ?i theo depth; ng??c l?i dùng movetime (?n ??nh h?n).
    /// </summary>
    public async Task<string> GetBestMoveAsync(
        string fen,
        string[] historyUci = null,
        int moveTimeMs = 1200,
        int? depth = null,
        CancellationToken external = default
    )
    {
        if (!IsReady) throw new Exception("Engine not started.");

        var moves = (historyUci != null && historyUci.Length > 0) ? " moves " + string.Join(" ", historyUci) : "";
        await SendAsync($"position fen {fen}{moves}");

        if (depth.HasValue) await SendAsync($"go depth {depth.Value}");
        else await SendAsync($"go movetime {moveTimeMs}");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, external);
        var sw = Stopwatch.StartNew();
        while (!linked.IsCancellationRequested)
        {
            if (_out.TryDequeue(out var line))
            {
                // Debug.Log("UCI << " + line);
                if (line.StartsWith("bestmove "))
                    return line.Split(' ')[1]; // e2e4 ho?c e7e8q
            }
            await Task.Yield();

            // Timeout safety
            if (sw.ElapsedMilliseconds > Math.Max(30000, (depth.HasValue ? 30000 : moveTimeMs + 5000)))
                throw new TimeoutException("Engine timed out.");
        }
        throw new OperationCanceledException();
    }

    public async Task QuitAsync()
    {
        try { await SendAsync("quit"); } catch { }

        try
        {
            if (_proc != null)
            {
                // N?u process còn s?ng thì kill theo API .NET 4.x c?a Unity
                if (!_proc.HasExited)
                {
                    try { _proc.Kill(); } catch { /* có th? ?ã thoát */ }
                }
                _proc.Dispose();
            }
        }
        catch { }

        try { _cts?.Cancel(); } catch { }
    }

    async Task SendAsync(string cmd)
    {
        // Debug.Log("UCI >> " + cmd);
        await _proc.StandardInput.WriteLineAsync(cmd);
        await _proc.StandardInput.FlushAsync();
    }

    async Task<bool> WaitForAsync(string token, int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (_out.TryDequeue(out var line))
            {
                // Debug.Log("UCI << " + line);
                if (line.Contains(token)) return true;
            }
            await Task.Yield();
        }
        return false;
    }

    public void Dispose() => _ = QuitAsync();
}
