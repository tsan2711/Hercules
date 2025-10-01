using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ChessBoardManager : MonoBehaviour, IChessBoard
{
    public static ChessBoardManager Instance;

    [Header("Chess Prefabs")]
    public GameObject whitePawnPrefab, whiteRookPrefab, whiteKnightPrefab, whiteBishopPrefab, whiteQueenPrefab, whiteKingPrefab;
    public GameObject blackPawnPrefab, blackRookPrefab, blackKnightPrefab, blackBishopPrefab, blackQueenPrefab, blackKingPrefab;

    [Header("Board Settings")]
    public float tileSize = 2f;
    public Transform boardFrame;       // A1 ở min XZ của mesh theo frame
    public Renderer boardRenderer;
    [HideInInspector] public ChessPieceInfo[,] board = new ChessPieceInfo[8, 8];

    float tileSizeX, tileSizeZ;
    public Vector3 boardOrigin = new Vector3(-7, 2, -7);

    [Header("Turn Management")]
    public bool isWhiteTurn = true;

    readonly List<string> moveHistoryUci = new List<string>();

    void Awake()
    {
        Instance = this;

        tileSizeX = tileSize;
        tileSizeZ = tileSize;

        if (boardFrame != null && boardRenderer != null)
        {
            var b = boardRenderer.bounds;
            Vector3 sizeLocal = AbsVec(boardFrame.InverseTransformVector(b.size));
            tileSizeX = sizeLocal.x / 8f;
            tileSizeZ = sizeLocal.z / 8f;

            Vector3 extLocal = AbsVec(boardFrame.InverseTransformVector(b.extents));
            Vector3 a1Local = new Vector3(-extLocal.x, 0f, -extLocal.z);
            Vector3 a1World = boardFrame.TransformPoint(a1Local);
            boardOrigin = new Vector3(a1World.x, boardOrigin.y, a1World.z);
        }
    }

    static Vector3 AbsVec(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    void Start() => SpawnBoard();

    void SpawnBoard()
    {
        // White
        SpawnPieceRow(whitePawnPrefab, true, 1, PieceType.Pawn);
        SpawnBackRow(true, 0);

        // Black
        SpawnPieceRow(blackPawnPrefab, false, 6, PieceType.Pawn);
        SpawnBackRow(false, 7);
    }

    void SpawnPieceRow(GameObject prefab, bool isWhite, int row, PieceType pieceType)
    {
        for (int col = 0; col < 8; col++)
        {
            Vector3 pos = BoardToWorld(col, row);
            Quaternion rot = isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0);

            var obj = Instantiate(prefab, pos, rot);
            var info = obj.GetComponent<ChessPieceInfo>();
            info.isWhite = isWhite;
            info.boardPosition = new Vector2Int(col, row);
            info.SetPieceType(pieceType); // đặt cả pieceType & type
            info.hasMoved = false;
            board[col, row] = info;
        }
    }

    void SpawnBackRow(bool isWhite, int row)
    {
        GameObject[] piecesOrder = isWhite
            ? new GameObject[] { whiteRookPrefab, whiteKnightPrefab, whiteBishopPrefab, whiteQueenPrefab, whiteKingPrefab, whiteBishopPrefab, whiteKnightPrefab, whiteRookPrefab }
            : new GameObject[] { blackRookPrefab, blackKnightPrefab, blackBishopPrefab, blackQueenPrefab, blackKingPrefab, blackBishopPrefab, blackKnightPrefab, blackRookPrefab };

        PieceType[] typeOrder =
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        for (int col = 0; col < 8; col++)
        {
            Vector3 pos = BoardToWorld(col, row);
            Quaternion rot = isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0);

            var obj = Instantiate(piecesOrder[col], pos, rot);
            var info = obj.GetComponent<ChessPieceInfo>();
            info.isWhite = isWhite;
            info.boardPosition = new Vector2Int(col, row);
            info.SetPieceType(typeOrder[col]); // đặt cả pieceType & type
            info.hasMoved = false;
            board[col, row] = info;
        }
    }

    // ===== Mapping =====
    public Vector3 BoardToWorld(int x, int y)
    {
        Vector3 local = new Vector3(x * tileSizeX, 0f, y * tileSizeZ);
        if (boardFrame != null) return boardFrame.TransformPoint(local);
        return new Vector3(boardOrigin.x + local.x, boardOrigin.y, boardOrigin.z + local.z);
    }

    public Vector2Int WorldToBoard(Vector3 worldPos)
    {
        Vector3 local = (boardFrame != null)
            ? boardFrame.InverseTransformPoint(worldPos)
            : new Vector3(worldPos.x - boardOrigin.x, 0f, worldPos.z - boardOrigin.z);

        int x = Mathf.FloorToInt(local.x / tileSizeX + 0.5f);
        int y = Mathf.FloorToInt(local.z / tileSizeZ + 0.5f);

        x = Mathf.Clamp(x, 0, 7);
        y = Mathf.Clamp(y, 0, 7);
        return new Vector2Int(x, y);
    }
    // ====================

    public void UpdateBoardPosition(ChessPieceInfo piece, Vector2Int newPos)
    {
        board[piece.boardPosition.x, piece.boardPosition.y] = null;
        board[newPos.x, newPos.y] = piece;
        piece.boardPosition = newPos;
        piece.transform.position = BoardToWorld(newPos.x, newPos.y);

        // Đánh dấu đã di chuyển cho logic nhập thành/pawn 2 bước
        piece.MarkMoved();
    }

    public bool CanPlayerMove(bool isWhite) => isWhite == isWhiteTurn;

    public void EndTurn()
    {
        isWhiteTurn = !isWhiteTurn;
        Debug.Log($"Turn switched to: {(isWhiteTurn ? "White" : "Black")}");
    }

    // ========= IChessBoard =========
    public string GetFen()
    {
        StringBuilder sb = new StringBuilder();
        for (int r = 7; r >= 0; r--)
        {
            int empty = 0;
            for (int f = 0; f < 8; f++)
            {
                var info = board[f, r];
                if (info == null) { empty++; }
                else
                {
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    sb.Append(GetFenChar(info));
                }
            }
            if (empty > 0) sb.Append(empty);
            if (r != 0) sb.Append('/');
        }

        sb.Append(isWhiteTurn ? " w " : " b ");
        sb.Append("- - ");
        sb.Append("0 ");
        int fullmove = 1 + (moveHistoryUci.Count / 2);
        sb.Append(fullmove);

        return sb.ToString();
    }

    public List<string> GetMoveHistoryUci() => moveHistoryUci;

    public void PlayMoveUci(string uci)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) { Debug.LogWarning("Invalid UCI: " + uci); return; }

        int fx = uci[0] - 'a';
        int fy = uci[1] - '1';
        int tx = uci[2] - 'a';
        int ty = uci[3] - '1';
        char promo = uci.Length > 4 ? uci[4] : '\0';

        if (fx < 0 || fx > 7 || fy < 0 || fy > 7 || tx < 0 || tx > 7 || ty < 0 || ty > 7)
        { Debug.LogWarning("UCI out of range: " + uci); return; }

        var piece = board[fx, fy];
        if (piece == null) { Debug.LogWarning($"No piece at {uci.Substring(0, 2)}"); return; }

        // Capture
        var target = board[tx, ty];
        if (target != null)
        {
            Destroy(target.gameObject);
            board[tx, ty] = null;
        }

        // Promotion?
        bool isPromotion = promo == 'q' || promo == 'r' || promo == 'b' || promo == 'n' ||
                           promo == 'Q' || promo == 'R' || promo == 'B' || promo == 'N';

        if (isPromotion && piece.pieceType == PieceType.Pawn)
        {
            bool isWhite = piece.isWhite;

            Destroy(piece.gameObject);
            board[fx, fy] = null;

            GameObject prefab = GetPromotionPrefab(isWhite, promo);
            var newObj = Instantiate(prefab, BoardToWorld(tx, ty), isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0));
            var newInfo = newObj.GetComponent<ChessPieceInfo>();
            newInfo.isWhite = isWhite;
            newInfo.boardPosition = new Vector2Int(tx, ty);
            newInfo.SetPieceType(CharToPieceType(promo)); // set pieceType & type
            newInfo.hasMoved = true; // quân mới coi như đã di chuyển
            board[tx, ty] = newInfo;
        }
        else
        {
            UpdateBoardPosition(piece, new Vector2Int(tx, ty));
        }

        moveHistoryUci.Add(uci);
        EndTurn();
    }

    public bool IsAiTurn() => !isWhiteTurn;
    public bool IsGameOver() => false;

    // ========= Helpers =========
    char GetFenChar(ChessPieceInfo info)
    {
        char c = 'p';
        switch (info.pieceType)
        {
            case PieceType.Pawn: c = 'p'; break;
            case PieceType.Rook: c = 'r'; break;
            case PieceType.Knight: c = 'n'; break;
            case PieceType.Bishop: c = 'b'; break;
            case PieceType.Queen: c = 'q'; break;
            case PieceType.King: c = 'k'; break;
        }
        if (info.isWhite) c = char.ToUpperInvariant(c);
        return c;
    }

    GameObject GetPromotionPrefab(bool isWhite, char promo)
    {
        switch (char.ToLowerInvariant(promo))
        {
            case 'q': return isWhite ? whiteQueenPrefab : blackQueenPrefab;
            case 'r': return isWhite ? whiteRookPrefab : blackRookPrefab;
            case 'b': return isWhite ? whiteBishopPrefab : blackBishopPrefab;
            case 'n': return isWhite ? whiteKnightPrefab : blackKnightPrefab;
            default: return isWhite ? whiteQueenPrefab : blackQueenPrefab;
        }
    }

    PieceType CharToPieceType(char promo)
    {
        switch (char.ToLowerInvariant(promo))
        {
            case 'q': return PieceType.Queen;
            case 'r': return PieceType.Rook;
            case 'b': return PieceType.Bishop;
            case 'n': return PieceType.Knight;
            default: return PieceType.Queen;
        }
    }
}
