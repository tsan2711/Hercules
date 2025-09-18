using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Threading;           // <- cho SemaphoreSlim
using System.Threading.Tasks;

public class UciEngine : IDisposable
{
    Process _proc;

    // NEW: khóa để đảm bảo chỉ 1 thao tác I/O với engine diễn ra tại một thời điểm
    readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);

    public async Task StartAsync(string enginePath)
    {
        _proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = enginePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            }
        };
        _proc.Start();

        await _ioLock.WaitAsync();
        try
        {
            await SendAsync("uci");
            await WaitForAnyAsync("uciok");
            await SendAsync("isready");
            await WaitForAnyAsync("readyok");
            await SendAsync("setoption name Threads value 2");
            await SendAsync("setoption name Hash value 128");
        }
        finally { _ioLock.Release(); }
    }

    public async Task SetStrengthAsync(int? uciElo = null, int? skill = null)
    {
        await _ioLock.WaitAsync();
        try
        {
            if (uciElo.HasValue)
            {
                await SendAsync("setoption name UCI_LimitStrength value true");
                await SendAsync($"setoption name UCI_Elo value {uciElo.Value}");
            }
            if (skill.HasValue)
            {
                await SendAsync($"setoption name Skill Level value {skill.Value}");
            }
            await SendAsync("isready");
            await WaitForAnyAsync("readyok");
        }
        finally { _ioLock.Release(); }
    }

    public async Task<string> GetBestMoveAsync(string startposOrFen, List<string> moves, int moveTimeMs = 600)
    {
        await _ioLock.WaitAsync();
        try
        {
            await PositionAsync(startposOrFen, moves);
            await SendAsync($"go movetime {moveTimeMs}");
            while (true)
            {
                var line = await _proc.StandardOutput.ReadLineAsync();
                if (line == null) break;
                if (line.StartsWith("bestmove "))
                    return line.Split(' ')[1];
            }
            return null;
        }
        finally { _ioLock.Release(); }
    }

    public async Task<List<string>> GetLegalMovesAsync(string startposOrFen, List<string> moves)
    {
        await _ioLock.WaitAsync();
        try
        {
            await PositionAsync(startposOrFen, moves);
            await SendAsync("d"); // in thông tin board
            var legal = new List<string>();
            while (true)
            {
                var line = await _proc.StandardOutput.ReadLineAsync();
                if (line == null) break;
                if (line.StartsWith("Legal moves:"))
                {
                    var part = line.Substring("Legal moves:".Length).Trim();
                    if (!string.IsNullOrEmpty(part))
                        legal.AddRange(part.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    break;
                }
                if (line.StartsWith("bestmove ")) break; // đề phòng
            }
            return legal;
        }
        finally { _ioLock.Release(); }
    }

    public async Task<string> GetFenAsync(string startposOrFen, List<string> moves)
    {
        await _ioLock.WaitAsync();
        try
        {
            await PositionAsync(startposOrFen, moves);
            await SendAsync("d");
            while (true)
            {
                var line = await _proc.StandardOutput.ReadLineAsync();
                if (line == null) break;
                if (line.StartsWith("Fen: "))
                    return line.Substring(5).Trim();
                if (line.StartsWith("bestmove ")) break;
            }
            return null;
        }
        finally { _ioLock.Release(); }
    }

    async Task PositionAsync(string startposOrFen, List<string> moves)
    {
        if (startposOrFen == "startpos")
        {
            if (moves != null && moves.Count > 0)
                await SendAsync($"position startpos moves {string.Join(" ", moves)}");
            else
                await SendAsync("position startpos");
        }
        else
        {
            if (moves != null && moves.Count > 0)
                await SendAsync($"position fen {startposOrFen} moves {string.Join(" ", moves)}");
            else
                await SendAsync($"position fen {startposOrFen}");
        }
        await SendAsync("isready");
        await WaitForAnyAsync("readyok");
    }

    async Task SendAsync(string cmd)
    {
        await _proc.StandardInput.WriteLineAsync(cmd);
        await _proc.StandardInput.FlushAsync();
    }

    async Task WaitForAnyAsync(string token)
    {
        while (true)
        {
            var line = await _proc.StandardOutput.ReadLineAsync();
            if (line == null) break;
            if (line.Contains(token)) return;
        }
    }

    public void Dispose()
    {
        try { _proc?.StandardInput.WriteLine("quit"); } catch { }
        if (_proc != null && !_proc.HasExited) _proc.Kill();
        _proc?.Dispose();
    }
}
