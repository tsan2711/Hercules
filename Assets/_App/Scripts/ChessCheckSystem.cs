using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class ChessCheckSystem : MonoBehaviour
{
    public static ChessCheckSystem Instance;
    
    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    public bool enableVisualDebug = true;
    
    [Header("Visual Debug")]
    public GameObject checkIndicatorPrefab;
    private GameObject currentCheckIndicator;
    
    void Awake()
    {
        Instance = this;
    }
    private async void Start()
    {
        // Đợi frame kế tiếp để chắc chắn ChessBoardManager đã Spawn xong
        await UniTask.Yield();

        if (ChessBoardManager.Instance == null)
        {
            Debug.LogError("ChessBoardManager chưa tồn tại!");
            return;
        }

        // Debug thử luôn trạng thái sau khi spawn
        TestGameState();
    }
    // ===================================
    // === CORE CHECK DETECTION =========
    // ===================================
    
    /// <summary>
    /// Kiểm tra xem vua có đang bị chiếu không
    /// </summary>
    public bool IsKingInCheck(bool isWhiteKing)
    {
        Vector2Int kingPos = FindKingPosition(isWhiteKing);
        if (kingPos == new Vector2Int(-1, -1))
        {
            DebugLog($"ERROR: Không tìm thấy vua {(isWhiteKing ? "Trắng" : "Đen")}!");
            return false;
        }
        
        bool inCheck = IsPositionAttackedBy(kingPos, !isWhiteKing);
        
        if (inCheck)
        {
            DebugLog($"CẢNH BÁO: Vua {(isWhiteKing ? "Trắng" : "Đen")} đang bị chiếu tại {kingPos}!");
            ShowCheckIndicator(kingPos);
        }
        else
        {
            HideCheckIndicator();
        }
        
        return inCheck;
    }
    
    /// <summary>
    /// Kiểm tra xem một vị trí có bị tấn công bởi quân đối phương không
    /// </summary>
    public bool IsPositionAttackedBy(Vector2Int position, bool attackerColor)
    {
        if (ChessBoardManager.Instance == null) return false;
        
        ChessPieceInfo[,] board = ChessBoardManager.Instance.board;
        
        // Duyệt qua tất cả quân cờ của màu tấn công
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = board[x, y];
                if (piece != null && piece.isWhite == attackerColor)
                {
                    if (CanPieceAttackPosition(piece, position))
                    {
                        DebugLog($"Vị trí {position} bị tấn công bởi {piece.type} {(piece.isWhite ? "Trắng" : "Đen")} tại {piece.boardPosition}");
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Kiểm tra xem một quân cờ có thể tấn công một vị trí không
    /// </summary>
    private bool CanPieceAttackPosition(ChessPieceInfo piece, Vector2Int targetPos)
    {
        switch (piece.type)
        {
            case ChessRaycastDebug.ChessType.Pawn:
                return CanPawnAttack(piece, targetPos);
            case ChessRaycastDebug.ChessType.Rook:
                return CanRookAttack(piece, targetPos);
            case ChessRaycastDebug.ChessType.Knight:
                return CanKnightAttack(piece, targetPos);
            case ChessRaycastDebug.ChessType.Bishop:
                return CanBishopAttack(piece, targetPos);
            case ChessRaycastDebug.ChessType.Queen:
                return CanQueenAttack(piece, targetPos);
            case ChessRaycastDebug.ChessType.King:
                return CanKingAttack(piece, targetPos);
        }
        return false;
    }
    
    // ===================================
    // === PIECE ATTACK PATTERNS ========
    // ===================================
    
    private bool CanPawnAttack(ChessPieceInfo pawn, Vector2Int target)
    {
        int dir = pawn.isWhite ? 1 : -1;
        Vector2Int pos = pawn.boardPosition;
        
        // Pawn chỉ tấn công chéo
        Vector2Int leftAttack = new Vector2Int(pos.x - 1, pos.y + dir);
        Vector2Int rightAttack = new Vector2Int(pos.x + 1, pos.y + dir);
        
        return target == leftAttack || target == rightAttack;
    }
    
    private bool CanRookAttack(ChessPieceInfo rook, Vector2Int target)
    {
        Vector2Int pos = rook.boardPosition;
        
        // Cùng hàng hoặc cùng cột
        if (pos.x != target.x && pos.y != target.y) return false;
        
        // Kiểm tra đường đi có bị cản không
        return HasClearPath(pos, target);
    }
    
    private bool CanBishopAttack(ChessPieceInfo bishop, Vector2Int target)
    {
        Vector2Int pos = bishop.boardPosition;
        
        // Kiểm tra đường chéo
        if (Mathf.Abs(pos.x - target.x) != Mathf.Abs(pos.y - target.y)) return false;
        
        return HasClearPath(pos, target);
    }
    
    private bool CanQueenAttack(ChessPieceInfo queen, Vector2Int target)
    {
        // Queen = Rook + Bishop
        return CanRookAttack(queen, target) || CanBishopAttack(queen, target);
    }
    
    private bool CanKnightAttack(ChessPieceInfo knight, Vector2Int target)
    {
        Vector2Int pos = knight.boardPosition;
        int dx = Mathf.Abs(pos.x - target.x);
        int dy = Mathf.Abs(pos.y - target.y);
        
        // Hình L: 2+1 hoặc 1+2
        return (dx == 2 && dy == 1) || (dx == 1 && dy == 2);
    }
    
    private bool CanKingAttack(ChessPieceInfo king, Vector2Int target)
    {
        Vector2Int pos = king.boardPosition;
        int dx = Mathf.Abs(pos.x - target.x);
        int dy = Mathf.Abs(pos.y - target.y);
        
        // Vua chỉ đi được 1 ô
        return dx <= 1 && dy <= 1 && (dx + dy) > 0;
    }
    
    // ===================================
    // === HELPER FUNCTIONS ==============
    // ===================================
    
    /// <summary>
    /// Kiểm tra đường đi từ start đến end có bị cản không (không tính 2 đầu)
    /// </summary>
    private bool HasClearPath(Vector2Int start, Vector2Int end)
    {
        if (ChessBoardManager.Instance == null) return false;
        
        int dx = end.x - start.x;
        int dy = end.y - start.y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        
        if (steps <= 1) return true; // Không có ô nào ở giữa
        
        int stepX = dx == 0 ? 0 : dx / Mathf.Abs(dx);
        int stepY = dy == 0 ? 0 : dy / Mathf.Abs(dy);
        
        ChessPieceInfo[,] board = ChessBoardManager.Instance.board;
        
        for (int i = 1; i < steps; i++)
        {
            int checkX = start.x + i * stepX;
            int checkY = start.y + i * stepY;
            
            if (board[checkX, checkY] != null)
            {
                return false; // Bị cản
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Tìm vị trí vua
    /// </summary>
    public Vector2Int FindKingPosition(bool isWhite)
    {
        if (ChessBoardManager.Instance == null) return new Vector2Int(-1, -1);
        
        ChessPieceInfo[,] board = ChessBoardManager.Instance.board;
        
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = board[x, y];
                if (piece != null && 
                    piece.isWhite == isWhite && 
                    piece.type == ChessRaycastDebug.ChessType.King)
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        
        return new Vector2Int(-1, -1); // Không tìm thấy
    }
    
    // ===================================
    // === LEGAL MOVES WITH CHECK =======
    // ===================================
    
    /// <summary>
    /// Lấy tất cả nước đi hợp lệ (loại bỏ những nước khiến vua bị chiếu)
    /// </summary>
    public List<Vector3> GetLegalMoves(ChessPieceInfo piece)
    {
        ChessMoveGenerator generator = FindObjectOfType<ChessMoveGenerator>();
        if (generator == null) return new List<Vector3>();
        
        List<Vector3> allMoves = generator.GetBasicMoves(piece);
        List<Vector3> legalMoves = new List<Vector3>();
        
        if (ChessBoardManager.Instance == null) return legalMoves;
        
        foreach (Vector3 worldMove in allMoves)
        {
            Vector2Int targetPos = ChessBoardManager.Instance.WorldToBoard(worldMove);
            
            if (IsMoveLegal(piece, targetPos))
            {
                legalMoves.Add(worldMove);
            }
        }
        
        DebugLog($"{piece.type} {(piece.isWhite ? "Trắng" : "Đen")} có {legalMoves.Count}/{allMoves.Count} nước đi hợp lệ");
        
        return legalMoves;
    }
    
    /// <summary>
    /// Kiểm tra nước đi có hợp lệ không (không làm vua bị chiếu)
    /// </summary>
    public bool IsMoveLegal(ChessPieceInfo piece, Vector2Int targetPos)
    {
        if (ChessBoardManager.Instance == null) return false;
        
        // Mô phỏng nước đi
        Vector2Int originalPos = piece.boardPosition;
        ChessPieceInfo capturedPiece = ChessBoardManager.Instance.board[targetPos.x, targetPos.y];
        
        // Thực hiện nước đi tạm thời
        ChessBoardManager.Instance.board[originalPos.x, originalPos.y] = null;
        ChessBoardManager.Instance.board[targetPos.x, targetPos.y] = piece;
        piece.boardPosition = targetPos;
        
        // Kiểm tra vua có bị chiếu không
        bool kingInCheck = IsKingInCheck(piece.isWhite);
        
        // Hoàn tác nước đi
        ChessBoardManager.Instance.board[targetPos.x, targetPos.y] = capturedPiece;
        ChessBoardManager.Instance.board[originalPos.x, originalPos.y] = piece;
        piece.boardPosition = originalPos;
        
        return !kingInCheck;
    }
    
    // ===================================
    // === CHECKMATE & STALEMATE ========
    // ===================================
    
    /// <summary>
    /// Kiểm tra chiếu bí
    /// </summary>
    public bool IsCheckmate(bool isWhiteKing)
    {
        DebugLog($"=== KIỂM TRA CHECKMATE CHO {(isWhiteKing ? "TRẮNG" : "ĐEN")} ===");
        
        // Phải đang bị chiếu và không có nước đi hợp lệ nào
        bool inCheck = IsKingInCheck(isWhiteKing);
        DebugLog($"Vua {(isWhiteKing ? "Trắng" : "Đen")} đang bị chiếu: {inCheck}");
        
        if (!inCheck) 
        {
            DebugLog("Không bị chiếu -> Không phải checkmate");
            return false;
        }
        
        bool hasLegalMoves = HasAnyLegalMoves(isWhiteKing);
        DebugLog($"Có nước đi hợp lệ: {hasLegalMoves}");
        
        bool isCheckmate = !hasLegalMoves;
        DebugLog($"KẾT QUẢ CHECKMATE: {isCheckmate}");
        
        return isCheckmate;
    }
    
    /// <summary>
    /// Kiểm tra hòa cờ (stalemate)
    /// </summary>
    public bool IsStalemate(bool isWhiteKing)
    {
        // Không bị chiếu nhưng không có nước đi hợp lệ nào
        if (IsKingInCheck(isWhiteKing)) return false;
        
        return !HasAnyLegalMoves(isWhiteKing);
    }
    
    /// <summary>
    /// Kiểm tra có nước đi hợp lệ nào không
    /// </summary>
    private bool HasAnyLegalMoves(bool isWhite)
    {
        if (ChessBoardManager.Instance == null) return true;
        
        ChessPieceInfo[,] board = ChessBoardManager.Instance.board;
        int totalPieces = 0;
        int piecesWithMoves = 0;
        
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = board[x, y];
                if (piece != null && piece.isWhite == isWhite)
                {
                    totalPieces++;
                    List<Vector3> legalMoves = GetLegalMoves(piece);
                    
                    DebugLog($"Checking {piece.type} {(piece.isWhite ? "Trắng" : "Đen")} tại {piece.boardPosition}: {legalMoves.Count} legal moves");
                    
                    if (legalMoves.Count > 0)
                    {
                        piecesWithMoves++;
                        DebugLog($"Tìm thấy {legalMoves.Count} nước đi hợp lệ cho {piece.type} tại {piece.boardPosition}");
                        return true;
                    }
                }
            }
        }
        
        DebugLog($"KIỂM TRA HOÀN TẤT: {totalPieces} quân {(isWhite ? "Trắng" : "Đen")}, {piecesWithMoves} quân có nước đi hợp lệ");
        
        if (piecesWithMoves == 0)
        {
            DebugLog($"KẾT QUẢ: Không có nước đi hợp lệ nào cho quân {(isWhite ? "Trắng" : "Đen")}! -> RETURN FALSE");
            return false;
        }
        else
        {
            DebugLog($"KẾT QUẢ: Có {piecesWithMoves} quân có nước đi hợp lệ -> RETURN TRUE");
            return true;
        }
    }
    
    // ===================================
    // === VISUAL DEBUG ==================
    // ===================================
    
    private void ShowCheckIndicator(Vector2Int kingPos)
    {
        if (!enableVisualDebug || checkIndicatorPrefab == null) return;
        
        HideCheckIndicator();
        
        Vector3 worldPos = ChessBoardManager.Instance.BoardToWorld(kingPos.x, kingPos.y);
        worldPos.y += 0f; // Nâng lên một chút để thấy rõ
        
        currentCheckIndicator = Instantiate(checkIndicatorPrefab, worldPos, Quaternion.Euler(-90, 0, 0));
    }
    
    private void HideCheckIndicator()
    {
        if (currentCheckIndicator != null)
        {
            Destroy(currentCheckIndicator);
            currentCheckIndicator = null;
        }
    }
    
    // ===================================
    // === GAME STATE CHECK ==============
    // ===================================
    
    /// <summary>
    /// Kiểm tra trạng thái game sau mỗi nước đi
    /// </summary>
    public async void CheckGameState()
    {
        await UniTask.Yield(); // đợi board update xong

        DebugLog("=== KIỂM TRA TRẠNG THÁI GAME ===");
        
        bool whiteInCheck = IsKingInCheck(true);
        bool blackInCheck = IsKingInCheck(false);
        
        DebugLog($"Vua Trắng bị chiếu: {whiteInCheck}");
        DebugLog($"Vua Đen bị chiếu: {blackInCheck}");

        // Kiểm tra checkmate
        if (whiteInCheck)
        {
            bool whiteCheckmate = IsCheckmate(true);
            DebugLog($"Vua Trắng checkmate: {whiteCheckmate}");
            if (whiteCheckmate)
            {
                DebugLog("CHIẾU BÍ! Quân Đen thắng!");
                OnGameEnd("Checkmate", false);
                return;
            }
        }
        
        if (blackInCheck)
        {
            bool blackCheckmate = IsCheckmate(false);
            DebugLog($"Vua Đen checkmate: {blackCheckmate}");
            if (blackCheckmate)
            {
                DebugLog("CHIẾU BÍ! Quân Trắng thắng!");
                OnGameEnd("Checkmate", true);
                return;
            }
        }
        
        // Kiểm tra stalemate
        if (ChessBoardManager.Instance != null)
        {
            bool currentPlayerIsWhite = ChessBoardManager.Instance.isWhiteTurn;
            bool stalemate = IsStalemate(currentPlayerIsWhite);
            DebugLog($"Stalemate cho {(currentPlayerIsWhite ? "Trắng" : "Đen")}: {stalemate}");
            
            if (stalemate)
            {
                DebugLog("HÒA CỜ! Không có nước đi hợp lệ.");
                OnGameEnd("Stalemate", null);
                return;
            }
        }
        
        // Chỉ bị chiếu
        if (whiteInCheck)
        {
            DebugLog("Vua Trắng đang bị chiếu!");
        }
        else if (blackInCheck)
        {
            DebugLog("Vua Đen đang bị chiếu!");
        }
        else
        {
            DebugLog("Không có tình huống đặc biệt nào.");
        }
    }

    
    private void OnGameEnd(string reason, bool? winner)
    {
        if (winner.HasValue)
        {
            Debug.Log($"=== GAME OVER ===\nLý do: {reason}\nNgười thắng: {(winner.Value ? "Trắng" : "Đen")}");
        }
        else
        {
            Debug.Log($"=== GAME OVER ===\nLý do: {reason}\nKết quả: Hòa");
        }
        
        // TODO: Implement game end UI hoặc restart logic
    }
    
    // ===================================
    // === DEBUG UTILITIES ===============
    // ===================================
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ChessCheck] {message}");
        }
    }
    
    /// <summary>
    /// Debug command để test các trường hợp
    /// </summary>
    [ContextMenu("Test Current Game State")]
    public void TestGameState()
    {
        Debug.Log("=== CHESS GAME STATE DEBUG ===");
        
        bool whiteCheck = IsKingInCheck(true);
        bool blackCheck = IsKingInCheck(false);
        
        Debug.Log($"Vua Trắng bị chiếu: {whiteCheck}");
        Debug.Log($"Vua Đen bị chiếu: {blackCheck}");
        
        if (whiteCheck) Debug.Log($"Vua Trắng chiếu bí: {IsCheckmate(true)}");
        if (blackCheck) Debug.Log($"Vua Đen chiếu bí: {IsCheckmate(false)}");
        
        Debug.Log($"Stalemate Trắng: {IsStalemate(true)}");
        Debug.Log($"Stalemate Đen: {IsStalemate(false)}");
        
        if (ChessBoardManager.Instance != null)
            Debug.Log($"Lượt hiện tại: {(ChessBoardManager.Instance.isWhiteTurn ? "Trắng" : "Đen")}");
    }
    
    /// <summary>
    /// Hiển thị tất cả nước đi hợp lệ của một quân cờ
    /// </summary>
    public void DebugPieceMoves(ChessPieceInfo piece)
    {
        if (piece == null) return;
        
        List<Vector3> legalMoves = GetLegalMoves(piece);
        
        Debug.Log($"=== DEBUG MOVES: {piece.type} {(piece.isWhite ? "Trắng" : "Đen")} tại {piece.boardPosition} ===");
        Debug.Log($"Số nước đi hợp lệ: {legalMoves.Count}");
        
        if (ChessBoardManager.Instance != null)
        {
            foreach (Vector3 move in legalMoves)
            {
                Vector2Int boardPos = ChessBoardManager.Instance.WorldToBoard(move);
                Debug.Log($"- Có thể đi đến: {boardPos}");
            }
        }
    }
    
    // ===================================
    // === KEYBOARD SHORTCUTS ============
    // ===================================

    void Update()
    {
        HandleDebugKeys();
    }
    
    void HandleDebugKeys()
    {
        // T - Test game state
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestGameState();
        }
        
        // G - Force check game state
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("Forcing CheckGameState()...");
            CheckGameState();
        }
        
        // C - Test checkmate
        if (Input.GetKeyDown(KeyCode.C))
        {
            TestCheckmate();
        }
        
        // M - Test checkmate for current player
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (ChessBoardManager.Instance != null)
            {
                bool isWhite = ChessBoardManager.Instance.isWhiteTurn;
                Debug.Log($"=== TESTING CHECKMATE FOR CURRENT PLAYER: {(isWhite ? "WHITE" : "BLACK")} ===");
                bool checkmate = IsCheckmate(isWhite);
                Debug.Log($"CHECKMATE RESULT: {checkmate}");
            }
        }
        
        // S - Test stalemate
        if (Input.GetKeyDown(KeyCode.S))
        {
            TestStalemate();
        }
        
        // D - Toggle debug logs
        if (Input.GetKeyDown(KeyCode.D))
        {
            enableDebugLogs = !enableDebugLogs;
            Debug.Log($"Debug logs: {(enableDebugLogs ? "ON" : "OFF")}");
        }
        
        // V - Toggle visual debug
        if (Input.GetKeyDown(KeyCode.V))
        {
            enableVisualDebug = !enableVisualDebug;
            Debug.Log($"Visual debug: {(enableVisualDebug ? "ON" : "OFF")}");
        }
    }
    
    void TestCheckmate()
    {
        Debug.Log("=== TESTING CHECKMATE ===");
        if (ChessBoardManager.Instance != null)
        {
            bool isWhite = ChessBoardManager.Instance.isWhiteTurn;
            bool checkmate = IsCheckmate(isWhite);
            Debug.Log($"Current player ({(isWhite ? "White" : "Black")}) checkmate: {checkmate}");
        }
    }
    
    void TestStalemate()
    {
        Debug.Log("=== TESTING STALEMATE ===");
        if (ChessBoardManager.Instance != null)
        {
            bool isWhite = ChessBoardManager.Instance.isWhiteTurn;
            bool stalemate = IsStalemate(isWhite);
            Debug.Log($"Current player ({(isWhite ? "White" : "Black")}) stalemate: {stalemate}");
        }
    }
}
