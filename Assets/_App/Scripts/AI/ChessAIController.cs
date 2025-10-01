using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// G?n script này vào 1 GameObject (ví d? GameManager).
/// Kéo component ChessBoardAdapter (implements IChessBoard) vào field Board Behaviour.
/// M?c ??nh AI ch?i ?EN (IsAiTurn() trong adapter tr? true khi t?i l??t ?en).
/// </summary>
public class ChessAIController : MonoBehaviour
{
    [Header("Board reference")]
    public MonoBehaviour boardBehaviour; // kéo ChessBoardAdapter vào
    IChessBoard board;

    [Header("Difficulty (ELO mode)")]
    [Tooltip("??t ELO 1350..2850. N?u mu?n dùng Skill Level thay vì ELO, ?? useSkillLevel=true bên d??i.")]
    public int elo = 1600;
    [Tooltip("Th?i gian suy ngh? m?i n??c (ms). ?n ??nh FPS h?n depth.")]
    public int moveTimeMs = 1200;
    [Tooltip("S? thread tính toán c?a engine.")]
    public int threads = 2;
    [Tooltip("Dung l??ng hash (MB) cho transposition table.")]
    public int hashMB = 128;
    [Tooltip("?? thiên v? c?a engine (-100..100). 0 là trung l?p.")]
    public int contempt = 0;

    [Header("Alternative difficulty (Skill Level 0..20)")]
    public bool useSkillLevel = false;
    [Range(0, 20)] public int skillLevel = 10;

    StockfishEngine engine;
    bool _thinking;

    async void Awake()
    {
        board = (IChessBoard)boardBehaviour;
        engine = new StockfishEngine();

        if (useSkillLevel)
            await engine.StartAsync(hashMB: hashMB, threads: threads, elo: null, skill: skillLevel, contempt: contempt);
        else
            await engine.StartAsync(hashMB: hashMB, threads: threads, elo: elo, skill: 20, contempt: contempt);
    }

    void Update()
    {
        if (board == null || engine == null) return;
        if (board.IsGameOver()) return;

        // T?i l??t AI và engine ch?a b?n => suy ngh? n??c ?i
        if (board.IsAiTurn() && engine.IsReady && !_thinking)
            _ = ThinkAndPlay();
    }

    async Task ThinkAndPlay()
    {
        _thinking = true;
        try
        {
            string fen = board.GetFen();
            var hist = board.GetMoveHistoryUci(); // có th? null
            string best = await engine.GetBestMoveAsync(fen, hist?.ToArray(), moveTimeMs);
            if (!string.IsNullOrEmpty(best))
                board.PlayMoveUci(best);
            else
                Debug.LogWarning("Engine returned empty bestmove.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError(ex);
        }
        finally
        {
            _thinking = false;
        }
    }

    async void OnDestroy()
    {
        if (engine != null) await engine.QuitAsync();
    }
}
