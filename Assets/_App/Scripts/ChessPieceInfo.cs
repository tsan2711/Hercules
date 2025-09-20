using UnityEngine;

public class ChessPieceInfo : MonoBehaviour
{
    public bool isWhite; // true = White, false = Black
    public ChessRaycastDebug.ChessType type;

    // Vị trí quân trên bàn cờ (cột, hàng)
    public Vector2Int boardPosition;
}
