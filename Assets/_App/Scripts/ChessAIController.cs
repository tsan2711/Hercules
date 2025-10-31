using System.Collections.Generic;
using UnityEngine;

public class ChessAIController : MonoBehaviour
{
    [Header("AI Settings")]
    public bool enableBlackAI = true;
    public bool enableWhiteAI = false; // giữ false để người chơi điều khiển trắng
    public float thinkDelaySeconds = 0.25f; // độ trễ nhỏ cho cảm giác tự nhiên

    [Header("References")]
    public ChessMoveGenerator moveGenerator;
    public ChessPieceMover pieceMover;

    private bool isThinking = false;
    private float nextAllowedThinkTime = 0f;

    void Awake()
    {
        if (moveGenerator == null) moveGenerator = FindObjectOfType<ChessMoveGenerator>();
        if (pieceMover == null) pieceMover = FindObjectOfType<ChessPieceMover>();
    }

    void Update()
    {
        if (ChessBoardManager.Instance == null || moveGenerator == null || pieceMover == null) return;

        // Chờ đến lượt AI và không có animation đang chạy
        bool isAITurn = (ChessBoardManager.Instance.isWhiteTurn && enableWhiteAI) || (!ChessBoardManager.Instance.isWhiteTurn && enableBlackAI);
        if (!isAITurn) { isThinking = false; return; }

        if (Time.time < nextAllowedThinkTime) return;
        if (IsAnyPieceBusy()) return;
        if (isThinking) return;

        isThinking = true;
        nextAllowedThinkTime = Time.time + thinkDelaySeconds;

        MakeAIMove(ChessBoardManager.Instance.isWhiteTurn);
        isThinking = false;
    }

    private void MakeAIMove(bool aiIsWhite)
    {
        // Thu thập tất cả quân của AI và nước đi hợp lệ
        List<(ChessPieceInfo piece, Vector3 move, int score)> candidateMoves = new List<(ChessPieceInfo, Vector3, int)>();

        ChessPieceInfo[,] board = ChessBoardManager.Instance.board;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = board[x, y];
                if (piece == null || piece.isWhite != aiIsWhite) continue;

                List<Vector3> legalMoves = ChessCheckSystem.Instance != null
                    ? ChessCheckSystem.Instance.GetLegalMoves(piece)
                    : moveGenerator.GetMoves(piece);

                foreach (var worldMove in legalMoves)
                {
                    Vector2Int target = ChessBoardManager.Instance.WorldToBoard(worldMove);
                    int score = ScoreMove(piece, target);
                    candidateMoves.Add((piece, worldMove, score));
                }
            }
        }

        if (candidateMoves.Count == 0)
        {
            Debug.Log("AI: No legal moves available.");
            return;
        }

        // Ưu tiên nước ăn quân, sau đó trung tâm, sau đó ngẫu nhiên
        int bestScore = int.MinValue;
        foreach (var c in candidateMoves) bestScore = Mathf.Max(bestScore, c.score);

        List<(ChessPieceInfo piece, Vector3 move, int score)> bestMoves = candidateMoves.FindAll(c => c.score == bestScore);
        var chosen = bestMoves[Random.Range(0, bestMoves.Count)];

        ExecuteMove(chosen.piece, chosen.move);
    }

    private int ScoreMove(ChessPieceInfo piece, Vector2Int target)
    {
        int score = 0;

        // Ăn quân có điểm
        ChessPieceInfo captured = ChessBoardManager.Instance.board[target.x, target.y];
        if (captured != null && captured.isWhite != piece.isWhite)
        {
            score += PieceValue(captured.type) * 10; // ưu tiên mạnh cho nước ăn
        }

        // Ưu tiên tiến vào trung tâm
        score += 4 - Mathf.Abs(3.5f - target.x) - Mathf.Abs(3.5f - target.y) > 0 ? 1 : 0;

        // Thưởng nhỏ nếu là nước đi của quân có giá trị thấp (an toàn)
        score += 5 - PieceValue(piece.type);

        return score;
    }

    private int PieceValue(ChessRaycastDebug.ChessType type)
    {
        switch (type)
        {
            case ChessRaycastDebug.ChessType.Pawn: return 1;
            case ChessRaycastDebug.ChessType.Knight: return 3;
            case ChessRaycastDebug.ChessType.Bishop: return 3;
            case ChessRaycastDebug.ChessType.Rook: return 5;
            case ChessRaycastDebug.ChessType.Queen: return 9;
            case ChessRaycastDebug.ChessType.King: return 100;
        }
        return 0;
    }

    private void ExecuteMove(ChessPieceInfo piece, Vector3 targetWorldPos)
    {
        // Dùng ChessPieceController nếu có, để đồng bộ với animation/sequence
        ChessPieceController controller = piece.GetComponent<ChessPieceController>();
        if (controller != null)
        {
            if (controller.IsBusy) return;
            controller.MovePiece(targetWorldPos);
        }
        else
        {
            // Fallback dùng ChessPieceMover
            pieceMover.MovePiece(piece, targetWorldPos);
        }
    }

    private bool IsAnyPieceBusy()
    {
        ChessPieceController[] controllers = FindObjectsOfType<ChessPieceController>();
        foreach (var c in controllers)
        {
            if (c.IsBusy) return true;
        }
        return false;
    }
}


