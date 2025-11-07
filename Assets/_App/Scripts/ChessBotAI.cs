using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AI Bot cho chế độ Level - tự động đánh cờ với độ khó tăng dần theo level
/// </summary>
public class ChessBotAI : MonoBehaviour
{
    public static ChessBotAI Instance;
    
    [Header("Bot Settings")]
    [SerializeField] private bool isLevelMode = false; // Chế độ level (chỉ player di chuyển)
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float botMoveDelay = 0.5f; // Delay trước khi bot đánh
    
    [Header("Difficulty Settings")]
    [SerializeField] private int minMaxDepthEasy = 2; // Level 1-3
    [SerializeField] private int minMaxDepthMedium = 3; // Level 4-6
    [SerializeField] private int minMaxDepthHard = 4; // Level 7-9
    [SerializeField] private int minMaxDepthExpert = 5; // Level 10+
    [SerializeField] private int quiescenceDepth = 3; // Depth cho quiescence search
    
    private bool isBotThinking = false;
    private Coroutine botMoveCoroutine;
    
    void Awake()
    {
        Instance = this;
    }
    
    void Start()
    {
        // Parse level từ scene name
        ParseLevelFromScene();
        
        // Kiểm tra xem có phải chế độ level không
        CheckLevelMode();
        
        // Subscribe to turn change event
        if (ChessBoardManager.Instance != null)
        {
            // Sẽ kiểm tra trong EndTurn của ChessBoardManager
        }
    }
    
    /// <summary>
    /// Parse level number từ scene name (ví dụ: "Level_1" -> 1)
    /// </summary>
    private void ParseLevelFromScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        
        if (sceneName.StartsWith("Level_"))
        {
            string levelStr = sceneName.Substring(6); // Bỏ qua "Level_"
            if (int.TryParse(levelStr, out int level))
            {
                currentLevel = level;
                isLevelMode = true;
                Debug.Log($"[ChessBotAI] Level mode activated - Level {currentLevel}");
            }
            else
            {
                Debug.LogWarning($"[ChessBotAI] Could not parse level from scene name: {sceneName}");
                isLevelMode = false;
            }
        }
        else
        {
            isLevelMode = false;
            Debug.Log($"[ChessBotAI] Not a level scene: {sceneName}");
        }
    }
    
    /// <summary>
    /// Kiểm tra xem có phải chế độ level không
    /// </summary>
    private void CheckLevelMode()
    {
        if (isLevelMode)
        {
            Debug.Log($"[ChessBotAI] Level Mode: ON - Level {currentLevel}");
        }
        else
        {
            Debug.Log("[ChessBotAI] Level Mode: OFF - Normal play mode");
        }
    }
    
    /// <summary>
    /// Được gọi khi lượt chơi kết thúc - kiểm tra xem có cần bot đánh không
    /// </summary>
    public void OnTurnEnded()
    {
        if (!isLevelMode) return;
        if (isBotThinking) return;
        
        // Nếu đến lượt bot (đen) và đang ở chế độ level
        if (ChessBoardManager.Instance != null && !ChessBoardManager.Instance.isWhiteTurn)
        {
            StartBotMove();
        }
    }
    
    /// <summary>
    /// Bắt đầu bot đánh
    /// </summary>
    private void StartBotMove()
    {
        if (botMoveCoroutine != null)
        {
            StopCoroutine(botMoveCoroutine);
        }
        
        botMoveCoroutine = StartCoroutine(BotMoveCoroutine());
    }
    
    /// <summary>
    /// Coroutine xử lý bot đánh
    /// </summary>
    private IEnumerator BotMoveCoroutine()
    {
        isBotThinking = true;
        
        // Delay trước khi bot đánh
        yield return new WaitForSeconds(botMoveDelay);
        
        // Lấy tất cả quân đen
        List<ChessPieceInfo> blackPieces = GetAllBlackPieces();
        
        if (blackPieces.Count == 0)
        {
            Debug.LogWarning("[ChessBotAI] No black pieces found!");
            isBotThinking = false;
            yield break;
        }
        
        // Chọn nước đi dựa trên độ khó
        ChessMove bestMove = GetBestMove(blackPieces);
        
        if (bestMove == null)
        {
            Debug.LogWarning("[ChessBotAI] No valid move found!");
            isBotThinking = false;
            yield break;
        }
        
        // Thực hiện nước đi
        ExecuteBotMove(bestMove);
        
        isBotThinking = false;
    }
    
    /// <summary>
    /// Lấy tất cả quân đen trên bàn cờ
    /// </summary>
    private List<ChessPieceInfo> GetAllBlackPieces()
    {
        List<ChessPieceInfo> pieces = new List<ChessPieceInfo>();
        
        if (ChessBoardManager.Instance == null) return pieces;
        
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = ChessBoardManager.Instance.board[x, y];
                if (piece != null && !piece.isWhite)
                {
                    pieces.Add(piece);
                }
            }
        }
        
        return pieces;
    }
    
    /// <summary>
    /// Chọn nước đi tốt nhất dựa trên độ khó
    /// </summary>
    private ChessMove GetBestMove(List<ChessPieceInfo> blackPieces)
    {
        int difficulty = GetDifficultyLevel();
        
        switch (difficulty)
        {
            case 1: // Level 1-3: Random move
                return GetRandomMove(blackPieces);
                
            case 2: // Level 4-6: Greedy (ăn quân nếu có thể)
                return GetGreedyMove(blackPieces);
                
            case 3: // Level 7-9: Minimax depth 2-3
                return GetMinimaxMove(blackPieces, minMaxDepthHard);
                
            case 4: // Level 10+: Minimax depth cao
                return GetMinimaxMove(blackPieces, minMaxDepthExpert);
                
            default:
                return GetRandomMove(blackPieces);
        }
    }
    
    /// <summary>
    /// Lấy độ khó dựa trên level
    /// </summary>
    private int GetDifficultyLevel()
    {
        if (currentLevel <= 3) return 1;      // Easy
        if (currentLevel <= 6) return 2;      // Medium
        if (currentLevel <= 9) return 3;      // Hard
        return 4;                              // Expert
    }
    
    /// <summary>
    /// Chọn nước đi ngẫu nhiên
    /// </summary>
    private ChessMove GetRandomMove(List<ChessPieceInfo> blackPieces)
    {
        List<ChessMove> allMoves = GetAllValidMoves(blackPieces);
        
        if (allMoves.Count == 0) return null;
        
        return allMoves[Random.Range(0, allMoves.Count)];
    }
    
    /// <summary>
    /// Chọn nước đi greedy (ưu tiên ăn quân, nhưng xem xét giá trị trao đổi)
    /// </summary>
    private ChessMove GetGreedyMove(List<ChessPieceInfo> blackPieces)
    {
        List<ChessMove> allMoves = GetAllValidMoves(blackPieces);
        
        if (allMoves.Count == 0) return null;
        
        // Sắp xếp moves theo thứ tự ưu tiên
        allMoves = OrderMoves(allMoves, blackPieces);
        
        // Ưu tiên nước đi ăn quân với giá trị trao đổi tốt
        List<ChessMove> goodCaptures = new List<ChessMove>();
        foreach (var move in allMoves)
        {
            if (move.isCapture)
            {
                // Tính giá trị trao đổi (exchange value)
                int captureValue = GetPieceValue(move.targetPiece);
                int attackerValue = GetPieceValue(move.piece);
                
                // Chỉ ăn nếu giá trị trao đổi tốt hoặc bằng
                if (captureValue >= attackerValue || IsSafeCapture(move))
                {
                    goodCaptures.Add(move);
                }
            }
        }
        
        if (goodCaptures.Count > 0)
        {
            // Chọn nước đi ăn quân có giá trị trao đổi tốt nhất
            ChessMove bestCapture = goodCaptures[0];
            int bestScore = EvaluateMoveScore(bestCapture);
            
            foreach (var move in goodCaptures)
            {
                int score = EvaluateMoveScore(move);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCapture = move;
                }
            }
            
            return bestCapture;
        }
        
        // Nếu không có nước đi ăn quân tốt, chọn nước đi tốt nhất (có thể là check, fork, etc.)
        ChessMove bestMove = allMoves[0];
        int bestMoveScore = EvaluateMoveScore(bestMove);
        
        foreach (var move in allMoves)
        {
            int score = EvaluateMoveScore(move);
            if (score > bestMoveScore)
            {
                bestMoveScore = score;
                bestMove = move;
            }
        }
        
        return bestMove;
    }
    
    /// <summary>
    /// Chọn nước đi bằng Minimax algorithm với move ordering
    /// </summary>
    private ChessMove GetMinimaxMove(List<ChessPieceInfo> blackPieces, int depth)
    {
        List<ChessMove> allMoves = GetAllValidMoves(blackPieces);
        
        if (allMoves.Count == 0) return null;
        
        // Sắp xếp moves để alpha-beta pruning hiệu quả hơn
        allMoves = OrderMoves(allMoves, blackPieces);
        
        ChessMove bestMove = null;
        int bestScore = int.MinValue;
        
        foreach (var move in allMoves)
        {
            // Thực hiện move tạm thời
            MakeTemporaryMove(move);
            
            // Tính điểm với quiescence search
            int score = Minimax(depth - 1, false, int.MinValue, int.MaxValue);
            
            // Hoàn tác move
            UndoTemporaryMove(move);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        
        return bestMove ?? allMoves[0]; // Fallback về move đầu tiên
    }
    
    /// <summary>
    /// Minimax algorithm với alpha-beta pruning và quiescence search
    /// </summary>
    private int Minimax(int depth, bool isMaximizing, int alpha, int beta)
    {
        if (depth == 0)
        {
            // Quiescence search: tiếp tục tìm kiếm trong captures và checks
            return QuiescenceSearch(quiescenceDepth, isMaximizing, alpha, beta);
        }
        
        if (isMaximizing) // Bot (đen) muốn tối đa hóa điểm
        {
            int maxScore = int.MinValue;
            List<ChessPieceInfo> blackPieces = GetAllBlackPieces();
            List<ChessMove> moves = GetAllValidMoves(blackPieces);
            
            // Sắp xếp moves để alpha-beta pruning hiệu quả hơn
            moves = OrderMoves(moves, blackPieces);
            
            foreach (var move in moves)
            {
                MakeTemporaryMove(move);
                int score = Minimax(depth - 1, false, alpha, beta);
                UndoTemporaryMove(move);
                
                maxScore = Mathf.Max(maxScore, score);
                alpha = Mathf.Max(alpha, score);
                
                if (beta <= alpha) break; // Alpha-beta pruning
            }
            
            return maxScore;
        }
        else // Player (trắng) muốn tối thiểu hóa điểm
        {
            int minScore = int.MaxValue;
            List<ChessPieceInfo> whitePieces = GetAllWhitePieces();
            List<ChessMove> moves = GetAllValidMoves(whitePieces);
            
            // Sắp xếp moves để alpha-beta pruning hiệu quả hơn
            moves = OrderMoves(moves, whitePieces);
            
            foreach (var move in moves)
            {
                MakeTemporaryMove(move);
                int score = Minimax(depth - 1, true, alpha, beta);
                UndoTemporaryMove(move);
                
                minScore = Mathf.Min(minScore, score);
                beta = Mathf.Min(beta, score);
                
                if (beta <= alpha) break; // Alpha-beta pruning
            }
            
            return minScore;
        }
    }
    
    /// <summary>
    /// Lấy tất cả quân trắng trên bàn cờ
    /// </summary>
    private List<ChessPieceInfo> GetAllWhitePieces()
    {
        List<ChessPieceInfo> pieces = new List<ChessPieceInfo>();
        
        if (ChessBoardManager.Instance == null) return pieces;
        
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = ChessBoardManager.Instance.board[x, y];
                if (piece != null && piece.isWhite)
                {
                    pieces.Add(piece);
                }
            }
        }
        
        return pieces;
    }
    
    /// <summary>
    /// Lấy tất cả nước đi hợp lệ cho danh sách quân cờ
    /// </summary>
    private List<ChessMove> GetAllValidMoves(List<ChessPieceInfo> pieces)
    {
        List<ChessMove> moves = new List<ChessMove>();
        
        if (ChessCheckSystem.Instance == null)
        {
            Debug.LogWarning("[ChessBotAI] ChessCheckSystem not found!");
            return moves;
        }
        
        foreach (var piece in pieces)
        {
            List<Vector3> validMoves = ChessCheckSystem.Instance.GetLegalMoves(piece);
            
            foreach (var movePos in validMoves)
            {
                Vector2Int targetPos = ChessBoardManager.Instance.WorldToBoard(movePos);
                ChessPieceInfo targetPiece = ChessBoardManager.Instance.board[targetPos.x, targetPos.y];
                
                ChessMove move = new ChessMove
                {
                    piece = piece,
                    targetPosition = movePos,
                    targetBoardPosition = targetPos,
                    targetPiece = targetPiece,
                    isCapture = targetPiece != null
                };
                
                moves.Add(move);
            }
        }
        
        return moves;
    }
    
    /// <summary>
    /// Đánh giá bàn cờ (điểm số cho bot) - nâng cấp với positional evaluation
    /// </summary>
    private int EvaluateBoard()
    {
        int score = 0;
        
        // Điểm số dựa trên giá trị quân cờ và vị trí
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = ChessBoardManager.Instance.board[x, y];
                if (piece != null)
                {
                    int value = GetPieceValue(piece);
                    int positionalValue = GetPositionalValue(piece, x, y);
                    int totalValue = value + positionalValue;
                    
                    if (piece.isWhite)
                    {
                        score -= totalValue; // Quân trắng làm giảm điểm
                    }
                    else
                    {
                        score += totalValue; // Quân đen làm tăng điểm
                    }
                }
            }
        }
        
        // Đánh giá mobility (số nước đi có thể)
        int blackMobility = GetMobility(false);
        int whiteMobility = GetMobility(true);
        score += (blackMobility - whiteMobility) * 2; // Mobility bonus
        
        // Đánh giá center control
        int centerControl = EvaluateCenterControl();
        score += centerControl;
        
        // Đánh giá piece safety
        int pieceSafety = EvaluatePieceSafety();
        score += pieceSafety;
        
        // Đánh giá king safety
        int kingSafety = EvaluateKingSafety();
        score += kingSafety;
        
        // Kiểm tra check/checkmate
        if (ChessCheckSystem.Instance != null)
        {
            if (ChessCheckSystem.Instance.IsKingInCheck(true))
            {
                score += 50; // Player bị check
            }
            if (ChessCheckSystem.Instance.IsKingInCheck(false))
            {
                score -= 50; // Bot bị check
            }
        }
        
        return score;
    }
    
    /// <summary>
    /// Lấy giá trị quân cờ
    /// </summary>
    private int GetPieceValue(ChessPieceInfo piece)
    {
        if (piece == null) return 0;
        
        switch (piece.type)
        {
            case ChessRaycastDebug.ChessType.Pawn: return 10;
            case ChessRaycastDebug.ChessType.Knight: return 30;
            case ChessRaycastDebug.ChessType.Bishop: return 30;
            case ChessRaycastDebug.ChessType.Rook: return 50;
            case ChessRaycastDebug.ChessType.Queen: return 90;
            case ChessRaycastDebug.ChessType.King: return 900;
            default: return 0;
        }
    }
    
    /// <summary>
    /// Thực hiện move tạm thời (cho minimax)
    /// </summary>
    private void MakeTemporaryMove(ChessMove move)
    {
        // Lưu trạng thái
        move.originalPosition = move.piece.boardPosition;
        move.capturedPiece = move.targetPiece;
        
        // Thực hiện move
        ChessBoardManager.Instance.board[move.originalPosition.x, move.originalPosition.y] = null;
        ChessBoardManager.Instance.board[move.targetBoardPosition.x, move.targetBoardPosition.y] = move.piece;
        move.piece.boardPosition = move.targetBoardPosition;
    }
    
    /// <summary>
    /// Hoàn tác move tạm thời
    /// </summary>
    private void UndoTemporaryMove(ChessMove move)
    {
        // Hoàn tác move
        ChessBoardManager.Instance.board[move.targetBoardPosition.x, move.targetBoardPosition.y] = move.capturedPiece;
        ChessBoardManager.Instance.board[move.originalPosition.x, move.originalPosition.y] = move.piece;
        move.piece.boardPosition = move.originalPosition;
    }
    
    /// <summary>
    /// Sắp xếp moves theo thứ tự ưu tiên (captures, checks, then others)
    /// </summary>
    private List<ChessMove> OrderMoves(List<ChessMove> moves, List<ChessPieceInfo> pieces)
    {
        List<ChessMove> orderedMoves = new List<ChessMove>();
        List<ChessMove> captures = new List<ChessMove>();
        List<ChessMove> checks = new List<ChessMove>();
        List<ChessMove> others = new List<ChessMove>();
        
        foreach (var move in moves)
        {
            // Kiểm tra xem có phải check không
            MakeTemporaryMove(move);
            bool isCheck = ChessCheckSystem.Instance != null && 
                          ChessCheckSystem.Instance.IsKingInCheck(!move.piece.isWhite);
            UndoTemporaryMove(move);
            
            if (move.isCapture)
            {
                captures.Add(move);
            }
            else if (isCheck)
            {
                checks.Add(move);
            }
            else
            {
                others.Add(move);
            }
        }
        
        // Sắp xếp captures theo giá trị trao đổi (MVV-LVA: Most Valuable Victim - Least Valuable Attacker)
        captures.Sort((a, b) => {
            int valueA = GetPieceValue(a.targetPiece) * 100 - GetPieceValue(a.piece);
            int valueB = GetPieceValue(b.targetPiece) * 100 - GetPieceValue(b.piece);
            return valueB.CompareTo(valueA);
        });
        
        orderedMoves.AddRange(captures);
        orderedMoves.AddRange(checks);
        orderedMoves.AddRange(others);
        
        return orderedMoves;
    }
    
    /// <summary>
    /// Kiểm tra xem capture có an toàn không (không bị ăn lại ngay)
    /// </summary>
    private bool IsSafeCapture(ChessMove move)
    {
        if (!move.isCapture) return false;
        
        MakeTemporaryMove(move);
        
        // Kiểm tra xem quân cờ có bị tấn công không
        bool isAttacked = ChessCheckSystem.Instance != null && 
                         ChessCheckSystem.Instance.IsPositionAttackedBy(
                             move.targetBoardPosition, !move.piece.isWhite);
        
        UndoTemporaryMove(move);
        
        return !isAttacked;
    }
    
    /// <summary>
    /// Đánh giá điểm của một move
    /// </summary>
    private int EvaluateMoveScore(ChessMove move)
    {
        int score = 0;
        
        // Capture value
        if (move.isCapture)
        {
            int captureValue = GetPieceValue(move.targetPiece);
            int attackerValue = GetPieceValue(move.piece);
            score += captureValue * 10 - attackerValue; // Ưu tiên ăn quân có giá trị cao
        }
        
        // Check bonus
        MakeTemporaryMove(move);
        bool isCheck = ChessCheckSystem.Instance != null && 
                      ChessCheckSystem.Instance.IsKingInCheck(!move.piece.isWhite);
        UndoTemporaryMove(move);
        
        if (isCheck)
        {
            score += 50; // Bonus cho check
        }
        
        // Positional value
        score += GetPositionalValue(move.piece, move.targetBoardPosition.x, move.targetBoardPosition.y);
        score -= GetPositionalValue(move.piece, move.originalPosition.x, move.originalPosition.y);
        
        // Center control
        if (IsCenterSquare(move.targetBoardPosition))
        {
            score += 5;
        }
        
        return score;
    }
    
    /// <summary>
    /// Quiescence search - tiếp tục tìm kiếm trong captures và checks
    /// </summary>
    private int QuiescenceSearch(int depth, bool isMaximizing, int alpha, int beta)
    {
        int standPat = EvaluateBoard();
        
        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;
        
        if (depth == 0) return standPat;
        
        List<ChessMove> moves;
        if (isMaximizing)
        {
            moves = GetAllValidMoves(GetAllBlackPieces());
        }
        else
        {
            moves = GetAllValidMoves(GetAllWhitePieces());
        }
        
        // Chỉ xem xét captures và checks trong quiescence search
        List<ChessMove> tacticalMoves = new List<ChessMove>();
        foreach (var move in moves)
        {
            if (move.isCapture)
            {
                tacticalMoves.Add(move);
            }
            else
            {
                // Kiểm tra check
                MakeTemporaryMove(move);
                bool isCheck = ChessCheckSystem.Instance != null && 
                              ChessCheckSystem.Instance.IsKingInCheck(!move.piece.isWhite);
                UndoTemporaryMove(move);
                
                if (isCheck)
                {
                    tacticalMoves.Add(move);
                }
            }
        }
        
        if (tacticalMoves.Count == 0) return standPat;
        
        // Sắp xếp moves
        tacticalMoves = OrderMoves(tacticalMoves, isMaximizing ? GetAllBlackPieces() : GetAllWhitePieces());
        
        foreach (var move in tacticalMoves)
        {
            MakeTemporaryMove(move);
            int score = QuiescenceSearch(depth - 1, !isMaximizing, -beta, -alpha);
            UndoTemporaryMove(move);
            
            score = -score; // Negamax
            
            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }
        
        return alpha;
    }
    
    /// <summary>
    /// Lấy giá trị vị trí của quân cờ (piece-square table)
    /// </summary>
    private int GetPositionalValue(ChessPieceInfo piece, int x, int y)
    {
        if (piece == null) return 0;
        
        // Piece-square tables - khuyến khích quân cờ ở vị trí tốt
        int[,] table = GetPieceSquareTable(piece.type, piece.isWhite);
        
        // Đảo ngược cho quân đen (y từ 0-7)
        int tableY = piece.isWhite ? y : 7 - y;
        
        return table[x, tableY];
    }
    
    /// <summary>
    /// Lấy piece-square table cho từng loại quân
    /// </summary>
    private int[,] GetPieceSquareTable(ChessRaycastDebug.ChessType type, bool isWhite)
    {
        int[,] table = new int[8, 8];
        
        switch (type)
        {
            case ChessRaycastDebug.ChessType.Pawn:
                // Pawn: khuyến khích ở center và tiến lên
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        int centerBonus = Mathf.Abs(x - 3.5f) < 2 ? 2 : 0;
                        int advanceBonus = y * 2;
                        table[x, y] = centerBonus + advanceBonus;
                    }
                }
                break;
                
            case ChessRaycastDebug.ChessType.Knight:
                // Knight: tốt nhất ở center
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        int centerX = Mathf.Abs(x - 3.5f);
                        int centerY = Mathf.Abs(y - 3.5f);
                        table[x, y] = 5 - (int)(centerX + centerY);
                    }
                }
                break;
                
            case ChessRaycastDebug.ChessType.Bishop:
                // Bishop: tốt ở center và đường chéo
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        int centerBonus = Mathf.Abs(x - 3.5f) < 2 && Mathf.Abs(y - 3.5f) < 2 ? 3 : 0;
                        table[x, y] = centerBonus;
                    }
                }
                break;
                
            case ChessRaycastDebug.ChessType.Rook:
                // Rook: tốt ở hàng/cột mở
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        table[x, y] = (y == 0 || y == 7) ? 2 : 0; // Khuyến khích ở hàng cuối
                    }
                }
                break;
                
            case ChessRaycastDebug.ChessType.Queen:
                // Queen: tốt ở center
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        int centerX = Mathf.Abs(x - 3.5f);
                        int centerY = Mathf.Abs(y - 3.5f);
                        table[x, y] = 3 - (int)(centerX + centerY) / 2;
                    }
                }
                break;
                
            case ChessRaycastDebug.ChessType.King:
                // King: an toàn ở góc trong opening, center trong endgame
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        // Trong endgame, khuyến khích vua ở center
                        table[x, y] = 2 - Mathf.Abs(x - 3.5f) - Mathf.Abs(y - 3.5f);
                    }
                }
                break;
        }
        
        return table;
    }
    
    /// <summary>
    /// Lấy mobility (số nước đi có thể)
    /// </summary>
    private int GetMobility(bool isWhite)
    {
        List<ChessPieceInfo> pieces = isWhite ? GetAllWhitePieces() : GetAllBlackPieces();
        int mobility = 0;
        
        foreach (var piece in pieces)
        {
            if (ChessCheckSystem.Instance != null)
            {
                List<Vector3> moves = ChessCheckSystem.Instance.GetLegalMoves(piece);
                mobility += moves.Count;
            }
        }
        
        return mobility;
    }
    
    /// <summary>
    /// Đánh giá kiểm soát trung tâm
    /// </summary>
    private int EvaluateCenterControl()
    {
        int blackControl = 0;
        int whiteControl = 0;
        
        // Center squares: d4, d5, e4, e5
        Vector2Int[] centerSquares = {
            new Vector2Int(3, 3), new Vector2Int(3, 4),
            new Vector2Int(4, 3), new Vector2Int(4, 4)
        };
        
        foreach (var square in centerSquares)
        {
            if (ChessCheckSystem.Instance != null)
            {
                if (ChessCheckSystem.Instance.IsPositionAttackedBy(square, false)) // Black attacks
                    blackControl++;
                if (ChessCheckSystem.Instance.IsPositionAttackedBy(square, true)) // White attacks
                    whiteControl++;
            }
        }
        
        return (blackControl - whiteControl) * 3;
    }
    
    /// <summary>
    /// Đánh giá an toàn của quân cờ
    /// </summary>
    private int EvaluatePieceSafety()
    {
        int score = 0;
        
        // Đánh giá quân đen
        List<ChessPieceInfo> blackPieces = GetAllBlackPieces();
        foreach (var piece in blackPieces)
        {
            if (ChessCheckSystem.Instance != null)
            {
                bool isAttacked = ChessCheckSystem.Instance.IsPositionAttackedBy(
                    piece.boardPosition, true);
                if (isAttacked)
                {
                    score -= GetPieceValue(piece) / 10; // Penalty cho quân bị tấn công
                }
            }
        }
        
        // Đánh giá quân trắng
        List<ChessPieceInfo> whitePieces = GetAllWhitePieces();
        foreach (var piece in whitePieces)
        {
            if (ChessCheckSystem.Instance != null)
            {
                bool isAttacked = ChessCheckSystem.Instance.IsPositionAttackedBy(
                    piece.boardPosition, false);
                if (isAttacked)
                {
                    score += GetPieceValue(piece) / 10; // Bonus cho quân địch bị tấn công
                }
            }
        }
        
        return score;
    }
    
    /// <summary>
    /// Đánh giá an toàn của vua
    /// </summary>
    private int EvaluateKingSafety()
    {
        int score = 0;
        
        // Tìm vua đen
        Vector2Int blackKingPos = FindKingPosition(false);
        if (blackKingPos.x >= 0)
        {
            int attackers = CountAttackers(blackKingPos, true);
            score -= attackers * 20; // Penalty cho mỗi quân tấn công vua
        }
        
        // Tìm vua trắng
        Vector2Int whiteKingPos = FindKingPosition(true);
        if (whiteKingPos.x >= 0)
        {
            int attackers = CountAttackers(whiteKingPos, false);
            score += attackers * 20; // Bonus cho mỗi quân tấn công vua địch
        }
        
        return score;
    }
    
    /// <summary>
    /// Tìm vị trí vua
    /// </summary>
    private Vector2Int FindKingPosition(bool isWhite)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = ChessBoardManager.Instance.board[x, y];
                if (piece != null && piece.isWhite == isWhite && 
                    piece.type == ChessRaycastDebug.ChessType.King)
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        return new Vector2Int(-1, -1);
    }
    
    /// <summary>
    /// Đếm số quân tấn công một vị trí
    /// </summary>
    private int CountAttackers(Vector2Int position, bool attackerColor)
    {
        int count = 0;
        
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = ChessBoardManager.Instance.board[x, y];
                if (piece != null && piece.isWhite == attackerColor)
                {
                    List<Vector3> moves = ChessCheckSystem.Instance.GetLegalMoves(piece);
                    foreach (var movePos in moves)
                    {
                        Vector2Int targetPos = ChessBoardManager.Instance.WorldToBoard(movePos);
                        if (targetPos == position)
                        {
                            count++;
                            break;
                        }
                    }
                }
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// Kiểm tra xem có phải ô trung tâm không
    /// </summary>
    private bool IsCenterSquare(Vector2Int pos)
    {
        return pos.x >= 3 && pos.x <= 4 && pos.y >= 3 && pos.y <= 4;
    }
    
    /// <summary>
    /// Thực hiện nước đi của bot
    /// </summary>
    private void ExecuteBotMove(ChessMove move)
    {
        if (move == null || move.piece == null)
        {
            Debug.LogError("[ChessBotAI] Invalid move!");
            return;
        }
        
        ChessPieceController pieceController = move.piece.GetComponent<ChessPieceController>();
        if (pieceController != null)
        {
            pieceController.MovePiece(move.targetPosition);
        }
        else
        {
            // Fallback
            ChessPieceMover pieceMover = FindObjectOfType<ChessPieceMover>();
            if (pieceMover != null)
            {
                pieceMover.MovePiece(move.piece, move.targetPosition);
            }
        }
        
        Debug.Log($"[ChessBotAI] Bot moved {move.piece.type} from {move.originalPosition} to {move.targetBoardPosition}");
    }
    
    /// <summary>
    /// Class đại diện cho một nước đi
    /// </summary>
    private class ChessMove
    {
        public ChessPieceInfo piece;
        public Vector3 targetPosition;
        public Vector2Int targetBoardPosition;
        public ChessPieceInfo targetPiece;
        public bool isCapture;
        public Vector2Int originalPosition;
        public ChessPieceInfo capturedPiece;
    }
    
    /// <summary>
    /// Kiểm tra xem có đang ở chế độ level không
    /// </summary>
    public bool IsLevelMode()
    {
        return isLevelMode;
    }
    
    /// <summary>
    /// Lấy level hiện tại
    /// </summary>
    public int GetCurrentLevel()
    {
        return currentLevel;
    }
}

