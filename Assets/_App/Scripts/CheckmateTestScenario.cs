using UnityEngine;

public class CheckmateTestScenario : MonoBehaviour
{
    void Update()
    {
        // F9 - Create simple checkmate scenario
        if (Input.GetKeyDown(KeyCode.F9))
        {
            CreateCheckmateScenario();
        }
        
        // F10 - Test checkmate detection
        if (Input.GetKeyDown(KeyCode.F10))
        {
            TestCheckmateDetection();
        }
    }
    
    [ContextMenu("Create Checkmate Scenario")]
    void CreateCheckmateScenario()
    {
        if (ChessBoardManager.Instance == null)
        {
            Debug.LogError("ChessBoardManager not found!");
            return;
        }
        
        Debug.Log("=== CREATING CHECKMATE SCENARIO ===");
        
        // Clear board except kings
        ClearBoardExceptKings();
        
        // Create a simple back-rank mate
        // Black King at h8 (7,7), hemmed in by own pawns
        // White Rook at a8 (0,7) to deliver checkmate
        
        PlacePiece(ChessRaycastDebug.ChessType.Pawn, false, 6, 6); // Black pawn at g7
        PlacePiece(ChessRaycastDebug.ChessType.Pawn, false, 7, 6); // Black pawn at h7
        PlacePiece(ChessRaycastDebug.ChessType.Rook, true, 0, 7);  // White rook at a8
        
        // Make sure black king is at h8
        Vector2Int blackKingPos = ChessCheckSystem.Instance.FindKingPosition(false);
        if (blackKingPos != new Vector2Int(7, 7))
        {
            // Move black king to h8
            MoveKingTo(false, 7, 7);
        }
        
        // Set turn to black so they're in checkmate
        ChessBoardManager.Instance.isWhiteTurn = false;
        
        Debug.Log("Checkmate scenario created! Black should be in checkmate.");
        Debug.Log("Press F10 to test checkmate detection.");
    }
    
    void TestCheckmateDetection()
    {
        if (ChessCheckSystem.Instance == null)
        {
            Debug.LogError("ChessCheckSystem not found!");
            return;
        }
        
        Debug.Log("=== TESTING CHECKMATE DETECTION ===");
        
        // Test both sides
        bool whiteCheckmate = ChessCheckSystem.Instance.IsCheckmate(true);
        bool blackCheckmate = ChessCheckSystem.Instance.IsCheckmate(false);
        
        Debug.Log($"White checkmate: {whiteCheckmate}");
        Debug.Log($"Black checkmate: {blackCheckmate}");
        
        // Force game state check
        if (ChessCheckSystem.Instance != null)
        {
            ChessCheckSystem.Instance.CheckGameState();
        }
    }
    
    void ClearBoardExceptKings()
    {
        if (ChessBoardManager.Instance == null) return;
        
        ChessPieceInfo[,] board = ChessBoardManager.Instance.board;
        
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceInfo piece = board[x, y];
                if (piece != null && piece.type != ChessRaycastDebug.ChessType.King)
                {
                    Destroy(piece.gameObject);
                    board[x, y] = null;
                }
            }
        }
    }
    
    void PlacePiece(ChessRaycastDebug.ChessType type, bool isWhite, int x, int y)
    {
        if (ChessBoardManager.Instance == null) return;
        
        ChessBoardManager boardManager = ChessBoardManager.Instance;
        
        // Remove existing piece
        if (boardManager.board[x, y] != null)
        {
            Destroy(boardManager.board[x, y].gameObject);
            boardManager.board[x, y] = null;
        }
        
        // Get appropriate prefab
        GameObject prefab = GetPrefabForType(type, isWhite);
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for {type} {(isWhite ? "White" : "Black")}");
            return;
        }
        
        // Create new piece
        Vector3 worldPos = boardManager.BoardToWorld(x, y);
        Quaternion rotation = isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
        
        GameObject newPiece = Instantiate(prefab, worldPos, rotation);
        ChessPieceInfo pieceInfo = newPiece.GetComponent<ChessPieceInfo>();
        
        pieceInfo.isWhite = isWhite;
        pieceInfo.type = type;
        pieceInfo.boardPosition = new Vector2Int(x, y);
        pieceInfo.hasMoved = true;
        
        boardManager.board[x, y] = pieceInfo;
        
        Debug.Log($"Placed {(isWhite ? "White" : "Black")} {type} at ({x}, {y})");
    }
    
    void MoveKingTo(bool isWhite, int x, int y)
    {
        if (ChessBoardManager.Instance == null) return;
        
        Vector2Int kingPos = ChessCheckSystem.Instance.FindKingPosition(isWhite);
        if (kingPos == new Vector2Int(-1, -1)) return;
        
        ChessPieceInfo king = ChessBoardManager.Instance.board[kingPos.x, kingPos.y];
        if (king == null) return;
        
        // Remove from old position
        ChessBoardManager.Instance.board[kingPos.x, kingPos.y] = null;
        
        // Remove piece at target position if any
        if (ChessBoardManager.Instance.board[x, y] != null)
        {
            Destroy(ChessBoardManager.Instance.board[x, y].gameObject);
        }
        
        // Move king
        Vector3 worldPos = ChessBoardManager.Instance.BoardToWorld(x, y);
        king.transform.position = worldPos;
        king.boardPosition = new Vector2Int(x, y);
        ChessBoardManager.Instance.board[x, y] = king;
        
        Debug.Log($"Moved {(isWhite ? "White" : "Black")} King to ({x}, {y})");
    }
    
    GameObject GetPrefabForType(ChessRaycastDebug.ChessType type, bool isWhite)
    {
        ChessBoardManager boardManager = ChessBoardManager.Instance;
        
        if (isWhite)
        {
            switch (type)
            {
                case ChessRaycastDebug.ChessType.Pawn: return boardManager.whitePawnPrefab;
                case ChessRaycastDebug.ChessType.Rook: return boardManager.whiteRookPrefab;
                case ChessRaycastDebug.ChessType.Knight: return boardManager.whiteKnightPrefab;
                case ChessRaycastDebug.ChessType.Bishop: return boardManager.whiteBishopPrefab;
                case ChessRaycastDebug.ChessType.Queen: return boardManager.whiteQueenPrefab;
                case ChessRaycastDebug.ChessType.King: return boardManager.whiteKingPrefab;
            }
        }
        else
        {
            switch (type)
            {
                case ChessRaycastDebug.ChessType.Pawn: return boardManager.blackPawnPrefab;
                case ChessRaycastDebug.ChessType.Rook: return boardManager.blackRookPrefab;
                case ChessRaycastDebug.ChessType.Knight: return boardManager.blackKnightPrefab;
                case ChessRaycastDebug.ChessType.Bishop: return boardManager.blackBishopPrefab;
                case ChessRaycastDebug.ChessType.Queen: return boardManager.blackQueenPrefab;
                case ChessRaycastDebug.ChessType.King: return boardManager.blackKingPrefab;
            }
        }
        
        return null;
    }
}
