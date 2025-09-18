using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class ChessController : MonoBehaviour
{
    [Header("References")]
    public ChessView view;              // Kéo Board (ChessView) vào đây

    [Header("Difficulty / Timing")]
    [Tooltip("Elo giả lập cho Stockfish (UCI_Elo)")]
    public int targetElo = 1200;
    [Tooltip("Thời gian suy nghĩ cho AI (ms)")]
    public int aiMoveTimeMs = 600;

    UciEngine _engine;
    readonly List<string> _moves = new List<string>();
    const string _start = "startpos";
    bool _busy = false;                  // chặn click khi đang xử lý/AI nghĩ

    async void Awake()
    {
        Application.runInBackground = true;

        // Đường dẫn engine
        string enginePath = Path.Combine(
            Application.streamingAssetsPath, "Engines",
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            "stockfish.exe"
#else
            "stockfish"
#endif
        );

        if (!File.Exists(enginePath))
        {
            Debug.LogError($"❌ Không tìm thấy Stockfish: {enginePath}");
            enabled = false;
            return;
        }

        try
        {
            _engine = new UciEngine();
            await _engine.StartAsync(enginePath);
            await _engine.SetStrengthAsync(uciElo: targetElo);
            await RefreshBoard();
            Debug.Log("✅ Stockfish sẵn sàng.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Khởi tạo engine lỗi: " + ex);
            enabled = false;
        }
    }

    /// <summary>Người chơi thử đi 1 nước UCI, ví dụ "e2e4". Nếu hợp lệ, AI sẽ đi lại.</summary>
    public async Task<bool> TryPlayerMove(string uciMove)
    {
        if (_engine == null || !enabled) return false;
        if (_busy) return false; // đang xử lý/AI đang nghĩ
        _busy = true;

        try
        {
            // Kiểm tra hợp lệ theo Stockfish
            var legal = await _engine.GetLegalMovesAsync(_start, _moves);
            if (!legal.Contains(uciMove))
            {
                Debug.LogWarning("❌ Nước không hợp lệ: " + uciMove);
                return false;
            }

            // Áp dụng nước của người chơi
            _moves.Add(uciMove);
            await RefreshBoard();

            // Hết nước cho bên còn lại?
            var nextLegal = await _engine.GetLegalMovesAsync(_start, _moves);
            if (nextLegal.Count == 0)
            {
                Debug.Log("🏁 Game over (sau nước người).");
                return true;
            }

            // Nước của AI
            var best = await _engine.GetBestMoveAsync(_start, _moves, aiMoveTimeMs);
            Debug.Log("🤖 AI bestmove: " + best);
            if (!string.IsNullOrEmpty(best))
            {
                _moves.Add(best);
                await RefreshBoard();
            }
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Lỗi khi xử lý nước đi: " + ex);
            return false;
        }
        finally
        {
            _busy = false;
        }
    }

    async Task RefreshBoard()
    {
        var fen = await _engine.GetFenAsync(_start, _moves);
        if (fen != null && view != null)
            view.RenderFen(fen);
    }

    void OnDestroy()
    {
        _engine?.Dispose();
    }

    // (Tuỳ chọn) gọi hàm này để bắt đầu ván mới
    public async void NewGame()
    {
        if (_engine == null) return;
        _moves.Clear();
        await RefreshBoard();
    }
}
