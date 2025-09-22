using UnityEngine;

public class ChessBoardManager : MonoBehaviour
{
    public static ChessBoardManager Instance; // Singleton để truy cập dễ từ MoveGenerator

    [Header("Chess Prefabs")]
    public GameObject whitePawnPrefab;
    public GameObject whiteRookPrefab;
    public GameObject whiteKnightPrefab;
    public GameObject whiteBishopPrefab;
    public GameObject whiteQueenPrefab;
    public GameObject whiteKingPrefab;

    public GameObject blackPawnPrefab;
    public GameObject blackRookPrefab;
    public GameObject blackKnightPrefab;
    public GameObject blackBishopPrefab;
    public GameObject blackQueenPrefab;
    public GameObject blackKingPrefab;

    [Header("Board Settings")]
    public float tileSize = 2f;
    public Vector3 boardOrigin = new Vector3(-7, 2, -7); // góc dưới trái bàn cờ (A1)
    [HideInInspector] public ChessPieceInfo[,] board = new ChessPieceInfo[8, 8];
    
    [Header("Turn Management")]
    public bool isWhiteTurn = true; // Quân trắng đi trước

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SpawnBoard();
    }

    void SpawnBoard()
    {
        // --- Trắng ---
        SpawnPieceRow(whitePawnPrefab, true, 1);
        SpawnBackRow(true, 0);

        // --- Đen ---
        SpawnPieceRow(blackPawnPrefab, false, 6);
        SpawnBackRow(false, 7);
    }

    void SpawnPieceRow(GameObject prefab, bool isWhite, int row)
    {
        for (int col = 0; col < 8; col++)
        {
            Vector3 pos = BoardToWorld(col, row);
            Quaternion rot = isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0);

            GameObject obj = Instantiate(prefab, pos, rot);
            ChessPieceInfo info = obj.GetComponent<ChessPieceInfo>();
            info.isWhite = isWhite;
            info.boardPosition = new Vector2Int(col, row);

            board[col, row] = info;
        }
    }

    void SpawnBackRow(bool isWhite, int row)
    {
        GameObject[] piecesOrder = isWhite
            ? new GameObject[] { whiteRookPrefab, whiteKnightPrefab, whiteBishopPrefab, whiteQueenPrefab, whiteKingPrefab, whiteBishopPrefab, whiteKnightPrefab, whiteRookPrefab }
            : new GameObject[] { blackRookPrefab, blackKnightPrefab, blackBishopPrefab, blackQueenPrefab, blackKingPrefab, blackBishopPrefab, blackKnightPrefab, blackRookPrefab };

        for (int col = 0; col < 8; col++)
        {
            Vector3 pos = BoardToWorld(col, row);
            Quaternion rot = isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0);

            GameObject obj = Instantiate(piecesOrder[col], pos, rot);
            ChessPieceInfo info = obj.GetComponent<ChessPieceInfo>();
            info.isWhite = isWhite;
            info.boardPosition = new Vector2Int(col, row);

            board[col, row] = info;
        }
    }

    // Chuyển từ tọa độ bàn cờ sang world position
    public Vector3 BoardToWorld(int x, int y)
    {
        return new Vector3(boardOrigin.x + x * tileSize, boardOrigin.y, boardOrigin.z + y * tileSize);
    }

    // Chuyển từ world position sang tọa độ bàn cờ (nếu cần)
    public Vector2Int WorldToBoard(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x - boardOrigin.x) / tileSize);
        int y = Mathf.RoundToInt((worldPos.z - boardOrigin.z) / tileSize);
        return new Vector2Int(x, y);
    }

    public void UpdateBoardPosition(ChessPieceInfo piece, Vector2Int newPos)
    {
        // Xóa vị trí cũ trên board
        board[piece.boardPosition.x, piece.boardPosition.y] = null;

        // Ghi vị trí mới
        board[newPos.x, newPos.y] = piece;
        piece.boardPosition = newPos;
    }
    
    // Turn Management Methods
    public bool CanPlayerMove(bool isWhite)
    {
        return isWhite == isWhiteTurn;
    }
    
    public void EndTurn()
    {
        isWhiteTurn = !isWhiteTurn;
        Debug.Log($"Turn switched to: {(isWhiteTurn ? "White" : "Black")}");
    }

}
