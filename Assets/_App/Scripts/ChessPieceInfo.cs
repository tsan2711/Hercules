using UnityEngine;

public class ChessPieceInfo : MonoBehaviour
{
    public bool isWhite; // true = White, false = Black
    public ChessRaycastDebug.ChessType type;
    public bool hasMoved = false; // true nếu quân cờ đã đi


    // Vị trí quân trên bàn cờ (cột, hàng)
    public Vector2Int boardPosition;
}
