using UnityEngine;

public class ChessBoardManager : MonoBehaviour
{
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
    public float tileSize = 2f; // Khoảng cách giữa các ô
    [HideInInspector]
    public ChessPieceInfo[,] board = new ChessPieceInfo[8, 8];

    void Start()
    {
        SpawnBoard();
    }

    void SpawnBoard()
    {
        // Vị trí bắt đầu
        Vector3 whitePawnStart = new Vector3(-7, 2, -5);
        Vector3 whiteBackStart = new Vector3(-7, 2, -7);

        Vector3 blackPawnStart = new Vector3(7, 2, 5);
        Vector3 blackBackStart = new Vector3(7, 2, 7);

        // Spawn quân trắng
        SpawnPieceRow(whitePawnPrefab, true, 0, whitePawnStart);
        SpawnBackRow(true, 0, whiteBackStart);

        // Spawn quân đen
        SpawnPieceRow(blackPawnPrefab, false, 0, blackPawnStart);
        SpawnBackRow(false, 0, blackBackStart);
    }

    void SpawnPieceRow(GameObject prefab, bool isWhite, int row, Vector3 startPos)
    {
        for (int col = 0; col < 8; col++)
        {
            float x = isWhite ? startPos.x + col * tileSize : startPos.x - col * tileSize;
            float y = startPos.y;
            float z = startPos.z;

            Vector3 pos = new Vector3(x, y, z);

            Quaternion rot = isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0);

            GameObject obj = Instantiate(prefab, pos, rot);
            ChessPieceInfo info = obj.GetComponent<ChessPieceInfo>();
            info.isWhite = isWhite;
            board[col, row] = info;
        }
    }

    void SpawnBackRow(bool isWhite, int row, Vector3 startPos)
    {
        GameObject[] piecesOrder = new GameObject[8];

        if (isWhite)
        {
            piecesOrder[0] = whiteRookPrefab;
            piecesOrder[1] = whiteKnightPrefab;
            piecesOrder[2] = whiteBishopPrefab;
            piecesOrder[3] = whiteQueenPrefab;
            piecesOrder[4] = whiteKingPrefab;
            piecesOrder[5] = whiteBishopPrefab;
            piecesOrder[6] = whiteKnightPrefab;
            piecesOrder[7] = whiteRookPrefab;
        }
        else
        {
            piecesOrder[0] = blackRookPrefab;
            piecesOrder[1] = blackKnightPrefab;
            piecesOrder[2] = blackBishopPrefab;
            piecesOrder[3] = blackQueenPrefab;
            piecesOrder[4] = blackKingPrefab;
            piecesOrder[5] = blackBishopPrefab;
            piecesOrder[6] = blackKnightPrefab;
            piecesOrder[7] = blackRookPrefab;
        }

        for (int col = 0; col < 8; col++)
        {
            float x = isWhite ? startPos.x + col * tileSize : startPos.x - col * tileSize;
            float y = startPos.y;
            float z = startPos.z;

            Vector3 pos = new Vector3(x, y, z);
            Quaternion rot = isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0);

            GameObject obj = Instantiate(piecesOrder[col], pos, rot);
            ChessPieceInfo info = obj.GetComponent<ChessPieceInfo>();
            info.isWhite = isWhite;
            board[col, row] = info;
        }
    }
}
