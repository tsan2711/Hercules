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
    [SerializeField] private int minMaxDepthEasy = 1; // Level 1-3: Depth thấp
    [SerializeField] private int minMaxDepthMedium = 2; // Level 4-6: Depth trung bình
    [SerializeField] private int minMaxDepthHard = 2; // Level 7-9: Giảm xuống 2 để tránh lag
    [SerializeField] private int minMaxDepthExpert = 3; // Level 10+: Giảm xuống 3 để tránh lag
    [SerializeField] private int quiescenceDepth = 1; // Giảm xuống 1 để tránh lag
    [SerializeField] private int maxQuiescenceDepth = 2; // Giảm xuống 2
    [SerializeField] private float maxThinkingTime = 2f; // Giảm xuống 2s để responsive hơn
    [SerializeField] private int maxMovesPerDepth = 20; // Giảm xuống 20 moves để tránh lag
    [SerializeField] private bool useMinimaxForAllLevels = true; // Dùng Minimax cho mọi level (trừ level 1-2)
    [SerializeField] private int yieldEveryNMoves = 1; // Yield sau mỗi N moves để tránh freeze
    
    private bool isBotThinking = false;
    private Coroutine botMoveCoroutine;
    private int moveCount = 0; // Đếm số lần bot di chuyển để tránh vòng lặp
    private bool shouldStopThinking = false; // Flag để dừng tính toán
    private float thinkingStartTime = 0f; // Thời gian bắt đầu suy nghĩ
    
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
        
        // Bảo vệ chống vòng lặp: reset moveCount nếu đã quá lâu
        if (moveCount > 100)
        {
            Debug.LogWarning("[ChessBotAI] Move count exceeded limit, resetting...");
            moveCount = 0;
            isBotThinking = false;
            return;
        }
        
        // Nếu đến lượt bot (đen) và đang ở chế độ level
        if (ChessBoardManager.Instance != null && !ChessBoardManager.Instance.isWhiteTurn)
        {
            moveCount++;
            StartBotMove();
        }
        else
        {
            // Reset moveCount khi đến lượt player
            moveCount = 0;
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
        
        shouldStopThinking = false;
        thinkingStartTime = 0f;
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
        
        // Kiểm tra lại xem có phải lượt bot không (tránh vòng lặp)
        if (ChessBoardManager.Instance == null || ChessBoardManager.Instance.isWhiteTurn)
        {
            Debug.LogWarning("[ChessBotAI] Not bot's turn anymore, aborting move");
            isBotThinking = false;
            moveCount = 0;
            yield break;
        }
        
        // Lấy tất cả quân đen
        List<ChessPieceInfo> blackPieces = GetAllBlackPieces();
        
        if (blackPieces.Count == 0)
        {
            Debug.LogWarning("[ChessBotAI] No black pieces found!");
            isBotThinking = false;
            moveCount = 0;
            yield break;
        }
        
        // Chọn nước đi dựa trên độ khó (chạy trong coroutine để tránh đứng editor)
        thinkingStartTime = Time.time;
        shouldStopThinking = false;
        
        ChessMove bestMove = null;
        int difficulty = GetDifficultyLevel();
        
        // Luôn dùng coroutine cho tất cả levels để tránh block
        yield return StartCoroutine(GetBestMoveCoroutineSafe(blackPieces, difficulty, (move) => {
            bestMove = move;
        }));
        
        // Fallback: nếu không tìm được move, dùng greedy hoặc random
        if (bestMove == null)
        {
            Debug.LogWarning("[ChessBotAI] No valid move found from Minimax, using fallback!");
            List<ChessMove> allMoves = GetAllValidMoves(blackPieces);
            if (allMoves.Count > 0)
            {
                // Dùng greedy move làm fallback
                if (difficulty >= 2)
                {
                    bestMove = GetGreedyMove(blackPieces);
                }
                else
                {
                    bestMove = GetRandomMove(blackPieces);
                }
            }
        }
        
        if (bestMove == null)
        {
            Debug.LogError("[ChessBotAI] No valid move found at all!");
            isBotThinking = false;
            moveCount = 0;
            yield break;
        }
        
        // QUAN TRỌNG: Validate lại move trước khi thực hiện
        // Vì move có thể đã bị thay đổi hoặc không còn hợp lệ sau khi được chọn
        if (bestMove != null && bestMove.piece != null)
        {
            // Kiểm tra xem quân cờ có còn ở vị trí ban đầu không
            if (bestMove.piece.boardPosition != bestMove.originalPosition)
            {
                Debug.LogWarning($"[ChessBotAI] Piece position changed before execution! Expected {bestMove.originalPosition}, got {bestMove.piece.boardPosition}. Re-fetching moves...");
                
                // Lấy lại moves hợp lệ từ vị trí hiện tại
                List<ChessMove> currentValidMoves = GetAllValidMoves(new List<ChessPieceInfo> { bestMove.piece });
                
                if (currentValidMoves.Count > 0)
                {
                    // Tìm move có targetBoardPosition giống với move ban đầu
                    ChessMove matchingMove = null;
                    foreach (var validMove in currentValidMoves)
                    {
                        if (validMove.targetBoardPosition == bestMove.targetBoardPosition)
                        {
                            matchingMove = validMove;
                            break;
                        }
                    }
                    
                    if (matchingMove != null)
                    {
                        bestMove = matchingMove;
                        Debug.Log($"[ChessBotAI] Found matching move after re-validation");
                    }
                    else
                    {
                        // Nếu không tìm thấy, dùng move đầu tiên hợp lệ
                        Debug.LogWarning($"[ChessBotAI] Original target not found, using first valid move");
                        bestMove = currentValidMoves[0];
                    }
                }
                else
                {
                    Debug.LogError($"[ChessBotAI] No valid moves found for piece at new position!");
                    isBotThinking = false;
                    moveCount = 0;
                    yield break;
                }
            }
        }
        
        // Thực hiện nước đi
        ExecuteBotMove(bestMove);
        
        // Đợi một frame để đảm bảo move đã được xử lý
        yield return null;
        
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
    /// Chọn nước đi tốt nhất dựa trên độ khó (chỉ dùng cho Easy/Medium, Hard/Expert dùng coroutine)
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
                
            case 3: // Level 7-9: Greedy (không dùng Minimax để tránh block)
            case 4: // Level 10+: Greedy (không dùng Minimax để tránh block)
                return GetGreedyMove(blackPieces);
                
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
    /// Chọn nước đi greedy đơn giản (tối ưu tốc độ)
    /// </summary>
    private ChessMove GetGreedyMove(List<ChessPieceInfo> blackPieces)
    {
        List<ChessMove> allMoves = GetAllValidMoves(blackPieces);
        
        if (allMoves.Count == 0) return null;
        
        // Sắp xếp moves đơn giản
        allMoves = OrderMoves(allMoves, blackPieces);
        
        ChessMove bestMove = null;
        int bestScore = int.MinValue;
        
        // Đánh giá đơn giản - KHÔNG gọi MakeTemporaryMove để tránh lag
        foreach (var move in allMoves)
        {
            int score = 0;
            
            // 1. Ưu tiên ăn quân (MVV-LVA)
            if (move.isCapture)
            {
                int captureValue = GetPieceValue(move.targetPiece);
                int attackerValue = GetPieceValue(move.piece);
                score += captureValue * 100 - attackerValue;
            }
            
            // 2. Positional value
            int positionGain = GetPositionalValue(move.piece, move.targetBoardPosition.x, move.targetBoardPosition.y);
            int positionLoss = GetPositionalValue(move.piece, move.originalPosition.x, move.originalPosition.y);
            score += (positionGain - positionLoss) * 5;
            
            // 3. Center control
            if (IsCenterSquare(move.targetBoardPosition))
            {
                score += 15;
            }
            
            // 4. Piece development
            if (move.originalPosition.y == 7)
            {
                score += 10;
            }
            
            // Cập nhật best move
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        
        return bestMove ?? allMoves[0];
    }
    
    /// <summary>
    /// Coroutine an toàn để lấy nước đi tốt nhất với timeout và fallback
    /// </summary>
    private IEnumerator GetBestMoveCoroutineSafe(List<ChessPieceInfo> blackPieces, int difficulty, System.Action<ChessMove> callback)
    {
        List<ChessMove> allMoves = GetAllValidMoves(blackPieces);
        if (allMoves.Count == 0)
        {
            callback(null);
            yield break;
        }
        
        // Nếu chỉ có 1 move, trả về ngay
        if (allMoves.Count == 1)
        {
            callback(allMoves[0]);
            yield break;
        }
        
        ChessMove bestMoveFound = null;
        
        // Level 1 (Difficulty 1): Random move - cho người mới chơi
        if (difficulty == 1)
        {
            Debug.Log("[ChessBotAI] Using RANDOM strategy (Level 1-3)");
            bestMoveFound = GetRandomMove(blackPieces);
            callback(bestMoveFound);
            yield break;
        }
        
        // Level 2 (Difficulty 2): Greedy - ưu tiên ăn quân
        if (difficulty == 2 && !useMinimaxForAllLevels)
        {
            Debug.Log("[ChessBotAI] Using GREEDY strategy (Level 4-6)");
            allMoves = OrderMoves(allMoves, blackPieces);
            bestMoveFound = GetGreedyMove(blackPieces);
            callback(bestMoveFound ?? allMoves[0]);
            yield break;
        }
        
        // Level 3+ (Difficulty 2+): Minimax với độ sâu tăng dần
        if (useMinimaxForAllLevels || difficulty >= 3)
        {
            // Chọn depth dựa trên difficulty
            int depth;
            string strategyName;
            
            if (difficulty == 1)
            {
                depth = minMaxDepthEasy;
                strategyName = "MINIMAX Easy";
            }
            else if (difficulty == 2)
            {
                depth = minMaxDepthMedium;
                strategyName = "MINIMAX Medium";
            }
            else if (difficulty == 3)
            {
                depth = minMaxDepthHard;
                strategyName = "MINIMAX Hard";
            }
            else
            {
                depth = minMaxDepthExpert;
                strategyName = "MINIMAX Expert";
            }
            
            Debug.Log($"[ChessBotAI] Using {strategyName} strategy with depth {depth}");
            Debug.Log($"[ChessBotAI] Performance: maxThinkingTime={maxThinkingTime}s, maxMoves={maxMovesPerDepth}, yield every {yieldEveryNMoves} moves");
            
            // Sắp xếp moves để alpha-beta pruning hiệu quả hơn
            allMoves = OrderMoves(allMoves, blackPieces);
            
            // Giới hạn số moves được xem xét
            int movesToCheck = Mathf.Min(allMoves.Count, maxMovesPerDepth);
            
            int bestScore = int.MinValue;
            int movesEvaluated = 0;
            float startTime = Time.time;
            
            Debug.Log($"[ChessBotAI] Evaluating {movesToCheck} out of {allMoves.Count} moves");
            
            for (int i = 0; i < movesToCheck; i++)
            {
                // Kiểm tra timeout
                if (shouldStopThinking || Time.time - thinkingStartTime > maxThinkingTime)
                {
                    Debug.Log($"[ChessBotAI] Timeout after {movesEvaluated} moves evaluated");
                    break;
                }
                
                ChessMove move = allMoves[i];
                
                // Validate move
                if (move.piece == null || move.piece.boardPosition != move.originalPosition)
                {
                    Debug.LogWarning($"[ChessBotAI] Skipping invalid move at index {i}");
                    continue;
                }
                
                // Thực hiện move tạm thời
                MakeTemporaryMove(move);
                
                // Tính điểm với Minimax và alpha-beta pruning
                int score = Minimax(depth - 1, false, int.MinValue, int.MaxValue);
                
                // Hoàn tác move
                UndoTemporaryMove(move);
                
                // Verify undo
                if (move.piece.boardPosition != move.originalPosition)
                {
                    Debug.LogError($"[ChessBotAI] Failed to undo move!");
                    move.piece.boardPosition = move.originalPosition;
                }
                
                movesEvaluated++;
                
                // Cập nhật best move
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMoveFound = move;
                    Debug.Log($"[ChessBotAI] New best move: {move.piece.type} from {move.originalPosition} to {move.targetBoardPosition}, score: {score}");
                }
                
                // QUAN TRỌNG: Yield SAU MỖI move để không bao giờ block editor
                if (i % yieldEveryNMoves == 0)
                {
                    yield return null;
                }
                
                // Kiểm tra timeout lại
                if (Time.time - thinkingStartTime > maxThinkingTime)
                {
                    Debug.Log($"[ChessBotAI] Timeout at move {i + 1}");
                    break;
                }
            }
            
            float elapsedTime = Time.time - startTime;
            Debug.Log($"[ChessBotAI] Finished evaluation: {movesEvaluated} moves in {elapsedTime:F2}s, best score: {bestScore}");
            
            if (elapsedTime > 1f)
            {
                Debug.LogWarning($"[ChessBotAI] Evaluation took {elapsedTime:F2}s! Consider reducing depth or maxMovesPerDepth");
            }
        }
        
        // Fallback nếu không tìm được move
        if (bestMoveFound == null)
        {
            Debug.LogWarning("[ChessBotAI] No move found from Minimax, using fallback");
            allMoves = OrderMoves(allMoves, blackPieces);
            bestMoveFound = GetGreedyMove(blackPieces) ?? allMoves[0];
        }
        
        callback(bestMoveFound);
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
        
        // Giới hạn số moves được xem xét
        int movesToCheck = Mathf.Min(allMoves.Count, maxMovesPerDepth);
        
        ChessMove bestMove = null;
        int bestScore = int.MinValue;
        
        for (int i = 0; i < movesToCheck; i++)
        {
            // Kiểm tra timeout
            if (shouldStopThinking || (thinkingStartTime > 0 && Time.time - thinkingStartTime > maxThinkingTime))
            {
                Debug.Log($"[ChessBotAI] Thinking timeout, using best move found so far");
                break;
            }
            
            ChessMove move = allMoves[i];
            
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
    /// Minimax algorithm với alpha-beta pruning (tối ưu để không lag)
    /// </summary>
    private int Minimax(int depth, bool isMaximizing, int alpha, int beta)
    {
        // Kiểm tra timeout - QUAN TRỌNG để tránh lag
        if (thinkingStartTime > 0 && Time.time - thinkingStartTime > maxThinkingTime * 0.8f)
        {
            return EvaluateBoard();
        }
        
        // Base case: đạt depth 0 hoặc depth âm
        if (depth <= 0)
        {
            return EvaluateBoard(); // Không dùng quiescence search để tránh lag
        }
        
        if (isMaximizing) // Bot (đen) muốn tối đa hóa điểm
        {
            int maxScore = int.MinValue;
            List<ChessPieceInfo> blackPieces = GetAllBlackPieces();
            List<ChessMove> moves = GetAllValidMoves(blackPieces);
            
            if (moves.Count == 0) return EvaluateBoard();
            
            // KHÔNG gọi OrderMoves ở đây để tránh lag - chỉ sort simple
            moves.Sort((a, b) => {
                int scoreA = a.isCapture ? GetPieceValue(a.targetPiece) : 0;
                int scoreB = b.isCapture ? GetPieceValue(b.targetPiece) : 0;
                return scoreB.CompareTo(scoreA);
            });
            
            // Giảm số moves xem xét ở depth sâu
            int movesToCheck = depth > 1 ? Mathf.Min(moves.Count, 10) : Mathf.Min(moves.Count, maxMovesPerDepth);
            
            for (int i = 0; i < movesToCheck; i++)
            {
                // Timeout check
                if (thinkingStartTime > 0 && Time.time - thinkingStartTime > maxThinkingTime * 0.8f)
                    break;
                
                ChessMove move = moves[i];
                
                // Validate
                if (move.piece == null || move.piece.boardPosition != move.originalPosition)
                    continue;
                
                MakeTemporaryMove(move);
                int score = Minimax(depth - 1, false, alpha, beta);
                UndoTemporaryMove(move);
                
                // Verify restore
                if (move.piece.boardPosition != move.originalPosition)
                    move.piece.boardPosition = move.originalPosition;
                
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
            
            if (moves.Count == 0) return EvaluateBoard();
            
            // KHÔNG gọi OrderMoves - chỉ sort simple
            moves.Sort((a, b) => {
                int scoreA = a.isCapture ? GetPieceValue(a.targetPiece) : 0;
                int scoreB = b.isCapture ? GetPieceValue(b.targetPiece) : 0;
                return scoreB.CompareTo(scoreA);
            });
            
            // Giảm số moves xem xét ở depth sâu
            int movesToCheck = depth > 1 ? Mathf.Min(moves.Count, 10) : Mathf.Min(moves.Count, maxMovesPerDepth);
            
            for (int i = 0; i < movesToCheck; i++)
            {
                // Timeout check
                if (thinkingStartTime > 0 && Time.time - thinkingStartTime > maxThinkingTime * 0.8f)
                    break;
                
                ChessMove move = moves[i];
                
                // Validate
                if (move.piece == null || move.piece.boardPosition != move.originalPosition)
                    continue;
                
                MakeTemporaryMove(move);
                int score = Minimax(depth - 1, true, alpha, beta);
                UndoTemporaryMove(move);
                
                // Verify restore
                if (move.piece.boardPosition != move.originalPosition)
                    move.piece.boardPosition = move.originalPosition;
                
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
            // QUAN TRỌNG: Lưu vị trí gốc của quân cờ trước khi lấy moves
            // ĐÂY là vị trí thực tế của quân cờ khi tạo move
            Vector2Int originalPos = piece.boardPosition;
            
            // Get legal moves - CHÚ Ý: GetLegalMoves có thể thay đổi board state!
            List<Vector3> validMoves = ChessCheckSystem.Instance.GetLegalMoves(piece);
            
            // Kiểm tra xem GetLegalMoves có làm thay đổi vị trí của quân cờ không
            if (piece.boardPosition != originalPos)
            {
                Debug.LogError($"[ChessBotAI] GetAllValidMoves: WARNING! GetLegalMoves changed piece position from {originalPos} to {piece.boardPosition}! This is a BUG in ChessCheckSystem!");
                // Force restore
                piece.boardPosition = originalPos;
            }
            
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
                    isCapture = targetPiece != null,
                    originalPosition = originalPos // QUAN TRỌNG: Set originalPosition ngay khi tạo move
                };
                
                // Verify ngay sau khi tạo
                if (move.originalPosition != piece.boardPosition)
                {
                    Debug.LogError($"[ChessBotAI] GetAllValidMoves: CRITICAL ERROR! originalPosition {move.originalPosition} != piece.boardPosition {piece.boardPosition} right after creation!");
                }
                
                moves.Add(move);
            }
        }
        
        return moves;
    }
    
    /// <summary>
    /// Đánh giá bàn cờ (tối ưu tốc độ - chỉ tính những thứ quan trọng nhất)
    /// </summary>
    private int EvaluateBoard()
    {
        int score = 0;
        
        // QUAN TRỌNG NHẤT: Điểm số dựa trên giá trị quân cờ và vị trí
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
                        score -= totalValue;
                    }
                    else
                    {
                        score += totalValue;
                    }
                }
            }
        }
        
        // Kiểm tra check/checkmate (rất quan trọng)
        if (ChessCheckSystem.Instance != null)
        {
            bool whiteInCheck = ChessCheckSystem.Instance.IsKingInCheck(true);
            bool blackInCheck = ChessCheckSystem.Instance.IsKingInCheck(false);
            
            if (whiteInCheck)
            {
                // Kiểm tra checkmate - ĐỪNG gọi IsCheckmate vì nó rất chậm
                score += 500; // Check bonus lớn
            }
            if (blackInCheck)
            {
                score -= 500; // Check penalty lớn
            }
        }
        
        // BỎ QUA các đánh giá phức tạp khác để tránh lag:
        // - Mobility (rất chậm vì phải GetLegalMoves cho tất cả quân)
        // - Piece safety (chậm vì phải check IsPositionAttackedBy)
        // - King safety (chậm)
        // - Center control (chậm)
        // - Pawn structure (chậm)
        
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
        if (move == null || move.piece == null)
        {
            Debug.LogError("[ChessBotAI] MakeTemporaryMove: move or piece is null!");
            return;
        }
        
        // KHÔNG bao giờ thay đổi originalPosition sau khi move đã được tạo
        // originalPosition phải được set khi tạo move và giữ nguyên
        // Chỉ lưu capturedPiece
        move.capturedPiece = move.targetPiece;
        
        // Validate positions
        if (move.originalPosition.x < 0 || move.originalPosition.x >= 8 || 
            move.originalPosition.y < 0 || move.originalPosition.y >= 8)
        {
            Debug.LogError($"[ChessBotAI] MakeTemporaryMove: Invalid original position {move.originalPosition}!");
            return;
        }
        
        if (move.targetBoardPosition.x < 0 || move.targetBoardPosition.x >= 8 || 
            move.targetBoardPosition.y < 0 || move.targetBoardPosition.y >= 8)
        {
            Debug.LogError($"[ChessBotAI] MakeTemporaryMove: Invalid target position {move.targetBoardPosition}!");
            return;
        }
        
        // Kiểm tra xem quân cờ có đang ở originalPosition không
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] MakeTemporaryMove: Piece not at original position! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            return;
        }
        
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
        if (move == null || move.piece == null)
        {
            Debug.LogError("[ChessBotAI] UndoTemporaryMove: move or piece is null!");
            return;
        }
        
        // Validate originalPosition
        if (move.originalPosition.x < 0 || move.originalPosition.x >= 8 || 
            move.originalPosition.y < 0 || move.originalPosition.y >= 8)
        {
            Debug.LogError($"[ChessBotAI] UndoTemporaryMove: Invalid original position {move.originalPosition}!");
            return;
        }
        
        // Validate target position
        if (move.targetBoardPosition.x < 0 || move.targetBoardPosition.x >= 8 || 
            move.targetBoardPosition.y < 0 || move.targetBoardPosition.y >= 8)
        {
            Debug.LogError($"[ChessBotAI] UndoTemporaryMove: Invalid target position {move.targetBoardPosition}!");
            return;
        }
        
        // Hoàn tác move
        ChessBoardManager.Instance.board[move.targetBoardPosition.x, move.targetBoardPosition.y] = move.capturedPiece;
        ChessBoardManager.Instance.board[move.originalPosition.x, move.originalPosition.y] = move.piece;
        move.piece.boardPosition = move.originalPosition;
        
        // Verify undo thành công
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] UndoTemporaryMove: Failed to restore piece position! Expected {move.originalPosition}, got {move.piece.boardPosition}");
        }
    }
    
    /// <summary>
    /// Sắp xếp moves đơn giản (chỉ captures, không check để tránh lag)
    /// </summary>
    private List<ChessMove> OrderMoves(List<ChessMove> moves, List<ChessPieceInfo> pieces)
    {
        // ĐƠN GIẢN HÓA: Chỉ sort theo capture value, KHÔNG check để tránh lag
        moves.Sort((a, b) => {
            // Captures trước
            if (a.isCapture && !b.isCapture) return -1;
            if (!a.isCapture && b.isCapture) return 1;
            
            // Nếu cả 2 đều capture, ưu tiên MVV-LVA
            if (a.isCapture && b.isCapture)
            {
                int valueA = GetPieceValue(a.targetPiece) * 100 - GetPieceValue(a.piece);
                int valueB = GetPieceValue(b.targetPiece) * 100 - GetPieceValue(b.piece);
                return valueB.CompareTo(valueA);
            }
            
            // Nếu không phải capture, ưu tiên center
            bool aCentral = IsCenterSquare(a.targetBoardPosition);
            bool bCentral = IsCenterSquare(b.targetBoardPosition);
            if (aCentral && !bCentral) return -1;
            if (!aCentral && bCentral) return 1;
            
            return 0;
        });
        
        return moves;
    }
    
    /// <summary>
    /// Kiểm tra xem move có phải fork không (tấn công nhiều quân cùng lúc)
    /// </summary>
    private bool IsFork(ChessMove move)
    {
        // Lưu trạng thái ban đầu
        Vector2Int originalPos = move.piece.boardPosition;
        
        // QUAN TRỌNG: Kiểm tra originalPosition trước khi make move
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] IsFork: Piece position mismatch before fork check! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            return false;
        }
        
        MakeTemporaryMove(move);
        
        int attackCount = 0;
        List<ChessPieceInfo> enemyPieces = move.piece.isWhite ? GetAllBlackPieces() : GetAllWhitePieces();
        
        // Sau khi move, quân cờ đã ở vị trí mới, kiểm tra các nước đi từ vị trí mới
        if (ChessCheckSystem.Instance != null)
        {
            List<Vector3> possibleMoves = ChessCheckSystem.Instance.GetLegalMoves(move.piece);
            foreach (var enemyPiece in enemyPieces)
            {
                foreach (var movePos in possibleMoves)
                {
                    Vector2Int targetPos = ChessBoardManager.Instance.WorldToBoard(movePos);
                    if (targetPos == enemyPiece.boardPosition)
                    {
                        attackCount++;
                        break;
                    }
                }
            }
        }
        
        UndoTemporaryMove(move);
        
        // Đảm bảo quân cờ trở về vị trí ban đầu
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] IsFork: Failed to restore piece position! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            // Force restore
            move.piece.boardPosition = move.originalPosition;
        }
        
        return attackCount >= 2; // Fork nếu tấn công 2+ quân
    }
    
    /// <summary>
    /// Kiểm tra xem move có tạo pin không (ghim quân địch)
    /// </summary>
    private bool IsPin(ChessMove move)
    {
        // QUAN TRỌNG: Kiểm tra originalPosition trước khi make move
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] IsPin: Piece position mismatch before pin check! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            return false;
        }
        
        // Pin xảy ra khi di chuyển quân và làm cho quân địch bị ghim vào vua
        // Đây là kiểm tra đơn giản, có thể cải thiện thêm
        MakeTemporaryMove(move);
        
        bool createsPin = false;
        List<ChessPieceInfo> enemyPieces = move.piece.isWhite ? GetAllBlackPieces() : GetAllWhitePieces();
        
        foreach (var enemyPiece in enemyPieces)
        {
            if (enemyPiece.type == ChessRaycastDebug.ChessType.King) continue;
            
            // Kiểm tra xem quân địch có bị ghim không
            // (kiểm tra xem có quân địch nào bị chặn đường đến vua không)
            if (ChessCheckSystem.Instance != null)
            {
                // Đơn giản hóa: nếu quân địch không thể di chuyển và vua bị check, có thể là pin
                List<Vector3> enemyMoves = ChessCheckSystem.Instance.GetLegalMoves(enemyPiece);
                if (enemyMoves.Count == 0 && ChessCheckSystem.Instance.IsKingInCheck(!move.piece.isWhite))
                {
                    createsPin = true;
                    break;
                }
            }
        }
        
        UndoTemporaryMove(move);
        
        // Đảm bảo quân cờ trở về vị trí ban đầu
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] IsPin: Failed to restore piece position! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            // Force restore
            move.piece.boardPosition = move.originalPosition;
        }
        
        return createsPin;
    }
    
    /// <summary>
    /// Kiểm tra xem capture có an toàn không (không bị ăn lại ngay)
    /// </summary>
    private bool IsSafeCapture(ChessMove move)
    {
        if (!move.isCapture) return false;
        
        // QUAN TRỌNG: Kiểm tra originalPosition trước khi make move
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] IsSafeCapture: Piece position mismatch before capture check! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            return false;
        }
        
        MakeTemporaryMove(move);
        
        // Kiểm tra xem quân cờ có bị tấn công không
        bool isAttacked = ChessCheckSystem.Instance != null && 
                         ChessCheckSystem.Instance.IsPositionAttackedBy(
                             move.targetBoardPosition, !move.piece.isWhite);
        
        UndoTemporaryMove(move);
        
        // Đảm bảo quân cờ trở về vị trí ban đầu
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] IsSafeCapture: Failed to restore piece position! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            // Force restore
            move.piece.boardPosition = move.originalPosition;
        }
        
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
        
        // QUAN TRỌNG: Kiểm tra originalPosition trước khi make move
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] EvaluateMoveScore: Piece position mismatch! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            return score; // Return current score without tactical evaluation
        }
        
        // Check bonus
        MakeTemporaryMove(move);
        bool isCheck = ChessCheckSystem.Instance != null && 
                      ChessCheckSystem.Instance.IsKingInCheck(!move.piece.isWhite);
        bool isCheckmate = isCheck && IsCheckmate(!move.piece.isWhite);
        UndoTemporaryMove(move);
        
        // Verify restore
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] EvaluateMoveScore: Failed to restore after check evaluation! Expected {move.originalPosition}, got {move.piece.boardPosition}");
            move.piece.boardPosition = move.originalPosition;
        }
        
        if (isCheckmate)
        {
            score += 10000; // Bonus cực lớn cho checkmate
        }
        else if (isCheck)
        {
            score += 150; // Tăng bonus cho check
        }
        
        // Fork bonus (tấn công nhiều quân)
        if (IsFork(move))
        {
            score += 80; // Bonus cho fork
        }
        
        // Pin bonus
        if (IsPin(move))
        {
            score += 40; // Bonus cho pin
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
        // Kiểm tra timeout
        if (thinkingStartTime > 0 && Time.time - thinkingStartTime > maxThinkingTime)
        {
            return EvaluateBoard();
        }
        
        // Giới hạn depth để tránh vòng lặp vô hạn
        if (depth <= 0 || depth > maxQuiescenceDepth)
        {
            return EvaluateBoard();
        }
        
        int standPat = EvaluateBoard();
        
        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;
        
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
    /// Lấy piece-square table cho từng loại quân (dựa trên chiến thuật cờ vua thực tế)
    /// </summary>
    private int[,] GetPieceSquareTable(ChessRaycastDebug.ChessType type, bool isWhite)
    {
        int[,] table = new int[8, 8];
        
        switch (type)
        {
            case ChessRaycastDebug.ChessType.Pawn:
                // Pawn: khuyến khích tiến lên, ở center, và promotion
                int[,] pawnTable = new int[,] {
                    { 0,  0,  0,  0,  0,  0,  0,  0 },
                    { 5, 10, 10,-20,-20, 10, 10,  5 },
                    { 5, -5,-10,  0,  0,-10, -5,  5 },
                    { 0,  0,  0, 20, 20,  0,  0,  0 },
                    { 5,  5, 10, 25, 25, 10,  5,  5 },
                    {10, 10, 20, 30, 30, 20, 10, 10 },
                    {50, 50, 50, 50, 50, 50, 50, 50 },
                    { 0,  0,  0,  0,  0,  0,  0,  0 }
                };
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        table[x, y] = pawnTable[y, x];
                break;
                
            case ChessRaycastDebug.ChessType.Knight:
                // Knight: tốt nhất ở center, tránh góc
                int[,] knightTable = new int[,] {
                    {-50,-40,-30,-30,-30,-30,-40,-50 },
                    {-40,-20,  0,  5,  5,  0,-20,-40 },
                    {-30,  5, 10, 15, 15, 10,  5,-30 },
                    {-30,  0, 15, 20, 20, 15,  0,-30 },
                    {-30,  5, 15, 20, 20, 15,  5,-30 },
                    {-30,  0, 10, 15, 15, 10,  0,-30 },
                    {-40,-20,  0,  0,  0,  0,-20,-40 },
                    {-50,-40,-30,-30,-30,-30,-40,-50 }
                };
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        table[x, y] = knightTable[y, x] / 10;
                break;
                
            case ChessRaycastDebug.ChessType.Bishop:
                // Bishop: tốt ở đường chéo dài và center
                int[,] bishopTable = new int[,] {
                    {-20,-10,-10,-10,-10,-10,-10,-20 },
                    {-10,  5,  0,  0,  0,  0,  5,-10 },
                    {-10, 10, 10, 10, 10, 10, 10,-10 },
                    {-10,  0, 10, 10, 10, 10,  0,-10 },
                    {-10,  5,  5, 10, 10,  5,  5,-10 },
                    {-10,  0,  5, 10, 10,  5,  0,-10 },
                    {-10,  0,  0,  0,  0,  0,  0,-10 },
                    {-20,-10,-10,-10,-10,-10,-10,-20 }
                };
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        table[x, y] = bishopTable[y, x] / 10;
                break;
                
            case ChessRaycastDebug.ChessType.Rook:
                // Rook: tốt ở hàng 7 (attacking) và cột mở
                int[,] rookTable = new int[,] {
                    { 0,  0,  0,  5,  5,  0,  0,  0 },
                    {-5,  0,  0,  0,  0,  0,  0, -5 },
                    {-5,  0,  0,  0,  0,  0,  0, -5 },
                    {-5,  0,  0,  0,  0,  0,  0, -5 },
                    {-5,  0,  0,  0,  0,  0,  0, -5 },
                    {-5,  0,  0,  0,  0,  0,  0, -5 },
                    { 5, 10, 10, 10, 10, 10, 10,  5 },
                    { 0,  0,  0,  0,  0,  0,  0,  0 }
                };
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        table[x, y] = rookTable[y, x] / 5;
                break;
                
            case ChessRaycastDebug.ChessType.Queen:
                // Queen: linh hoạt, ở center nhưng không quá sớm
                int[,] queenTable = new int[,] {
                    {-20,-10,-10, -5, -5,-10,-10,-20 },
                    {-10,  0,  5,  0,  0,  0,  0,-10 },
                    {-10,  5,  5,  5,  5,  5,  0,-10 },
                    {  0,  0,  5,  5,  5,  5,  0, -5 },
                    { -5,  0,  5,  5,  5,  5,  0, -5 },
                    {-10,  0,  5,  5,  5,  5,  0,-10 },
                    {-10,  0,  0,  0,  0,  0,  0,-10 },
                    {-20,-10,-10, -5, -5,-10,-10,-20 }
                };
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        table[x, y] = queenTable[y, x] / 10;
                break;
                
            case ChessRaycastDebug.ChessType.King:
                // King: an toàn ở góc, có castle protection
                int[,] kingTable = new int[,] {
                    { 20, 30, 10,  0,  0, 10, 30, 20 },
                    { 20, 20,  0,  0,  0,  0, 20, 20 },
                    {-10,-20,-20,-20,-20,-20,-20,-10 },
                    {-20,-30,-30,-40,-40,-30,-30,-20 },
                    {-30,-40,-40,-50,-50,-40,-40,-30 },
                    {-30,-40,-40,-50,-50,-40,-40,-30 },
                    {-30,-40,-40,-50,-50,-40,-40,-30 },
                    {-30,-40,-40,-50,-50,-40,-40,-30 }
                };
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        table[x, y] = kingTable[y, x] / 10;
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
    /// Kiểm tra xem có phải checkmate không
    /// </summary>
    private bool IsCheckmate(bool isWhite)
    {
        if (ChessCheckSystem.Instance == null) return false;
        
        // Phải đang bị check
        if (!ChessCheckSystem.Instance.IsKingInCheck(isWhite)) return false;
        
        // Kiểm tra xem có nước đi hợp lệ nào không
        List<ChessPieceInfo> pieces = isWhite ? GetAllWhitePieces() : GetAllBlackPieces();
        List<ChessMove> moves = GetAllValidMoves(pieces);
        
        return moves.Count == 0; // Không có nước đi hợp lệ = checkmate
    }
    
    /// <summary>
    /// Đánh giá cấu trúc tốt (pawn structure)
    /// </summary>
    private int EvaluatePawnStructure()
    {
        int score = 0;
        
        // Đánh giá tốt đen
        List<ChessPieceInfo> blackPawns = new List<ChessPieceInfo>();
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = ChessBoardManager.Instance.board[x, y];
                if (piece != null && !piece.isWhite && piece.type == ChessRaycastDebug.ChessType.Pawn)
                {
                    blackPawns.Add(piece);
                }
            }
        }
        
        // Đánh giá tốt trắng
        List<ChessPieceInfo> whitePawns = new List<ChessPieceInfo>();
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = ChessBoardManager.Instance.board[x, y];
                if (piece != null && piece.isWhite && piece.type == ChessRaycastDebug.ChessType.Pawn)
                {
                    whitePawns.Add(piece);
                }
            }
        }
        
        // Penalty cho tốt bị cô lập (isolated pawns)
        foreach (var pawn in blackPawns)
        {
            bool hasFriendlyPawn = false;
            int pawnX = pawn.boardPosition.x;
            
            // Kiểm tra tốt cùng màu ở cột bên cạnh
            for (int x = Mathf.Max(0, pawnX - 1); x <= Mathf.Min(7, pawnX + 1); x++)
            {
                if (x == pawnX) continue;
                
                for (int y = 0; y < 8; y++)
                {
                    ChessPieceInfo p = ChessBoardManager.Instance.board[x, y];
                    if (p != null && !p.isWhite && p.type == ChessRaycastDebug.ChessType.Pawn)
                    {
                        hasFriendlyPawn = true;
                        break;
                    }
                }
                if (hasFriendlyPawn) break;
            }
            
            if (!hasFriendlyPawn)
            {
                score -= 10; // Penalty cho tốt cô lập
            }
        }
        
        foreach (var pawn in whitePawns)
        {
            bool hasFriendlyPawn = false;
            int pawnX = pawn.boardPosition.x;
            
            for (int x = Mathf.Max(0, pawnX - 1); x <= Mathf.Min(7, pawnX + 1); x++)
            {
                if (x == pawnX) continue;
                
                for (int y = 0; y < 8; y++)
                {
                    ChessPieceInfo p = ChessBoardManager.Instance.board[x, y];
                    if (p != null && p.isWhite && p.type == ChessRaycastDebug.ChessType.Pawn)
                    {
                        hasFriendlyPawn = true;
                        break;
                    }
                }
                if (hasFriendlyPawn) break;
            }
            
            if (!hasFriendlyPawn)
            {
                score += 10; // Bonus cho tốt địch cô lập
            }
        }
        
        return score;
    }
    
    /// <summary>
    /// Thực hiện nước đi của bot
    /// </summary>
    private void ExecuteBotMove(ChessMove move)
    {
        if (move == null || move.piece == null)
        {
            Debug.LogError("[ChessBotAI] Invalid move!");
            isBotThinking = false;
            moveCount = 0;
            return;
        }
        
        // Kiểm tra lại xem có phải lượt bot không
        if (ChessBoardManager.Instance == null || ChessBoardManager.Instance.isWhiteTurn)
        {
            Debug.LogWarning("[ChessBotAI] Not bot's turn, aborting move execution");
            isBotThinking = false;
            moveCount = 0;
            return;
        }
        
        // QUAN TRỌNG: Validate move trước khi thực hiện
        // Kiểm tra xem quân cờ có đang ở vị trí đúng không
        if (move.piece.boardPosition != move.originalPosition)
        {
            Debug.LogError($"[ChessBotAI] CRITICAL: Piece position mismatch before execution! Piece {move.piece.type} should be at {move.originalPosition} but is at {move.piece.boardPosition}");
            Debug.LogError($"[ChessBotAI] This indicates the board state was modified during move selection!");
            isBotThinking = false;
            moveCount = 0;
            return;
        }
        
        // Validation cuối cùng: Kiểm tra xem targetPosition có nằm trong danh sách moves hợp lệ không
        if (ChessCheckSystem.Instance != null)
        {
            List<Vector3> validMoves = ChessCheckSystem.Instance.GetLegalMoves(move.piece);
            bool moveIsValid = false;
            
            foreach (var validMovePos in validMoves)
            {
                Vector2Int validTargetPos = ChessBoardManager.Instance.WorldToBoard(validMovePos);
                if (validTargetPos == move.targetBoardPosition)
                {
                    moveIsValid = true;
                    // Sử dụng world position từ valid moves để đảm bảo chính xác
                    move.targetPosition = validMovePos;
                    break;
                }
            }
            
            if (!moveIsValid)
            {
                Debug.LogError($"[ChessBotAI] CRITICAL ERROR: Move validation failed!");
                Debug.LogError($"[ChessBotAI] Piece: {move.piece.type} at {move.piece.boardPosition}, Target: {move.targetBoardPosition}");
                Debug.LogError($"[ChessBotAI] Original position: {move.originalPosition}");
                Debug.LogError($"[ChessBotAI] Valid moves for this piece:");
                foreach (var validMovePos in validMoves)
                {
                    Vector2Int validTargetPos = ChessBoardManager.Instance.WorldToBoard(validMovePos);
                    Debug.LogError($"  - {validTargetPos}");
                }
                
                Debug.LogError($"[ChessBotAI] This should NEVER happen if GetAllValidMoves works correctly!");
                Debug.LogError($"[ChessBotAI] The move was selected from valid moves but is no longer valid at execution time.");
                
                isBotThinking = false;
                moveCount = 0;
                return;
            }
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

