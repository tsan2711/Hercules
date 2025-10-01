using UnityEngine;

// Enum nội bộ cho FEN/logic
public enum PieceType { Pawn, Rook, Knight, Bishop, Queen, King }

public class ChessPieceInfo : MonoBehaviour
{
    [Header("State")]
    public bool isWhite;
    public Vector2Int boardPosition;

    [Header("Core (internal)")]
    // Đây là nguồn sự thật nội bộ mà ChessBoardManager sẽ dùng
    public PieceType pieceType = PieceType.Pawn;

    [Header("Legacy flags for other scripts")]
    public bool hasMoved = false;

    // ======= TƯƠNG THÍCH NGƯỢC =======
    // Property 'type' GIỮ NGUYÊN KIỂU cũ: ChessRaycastDebug.ChessType
    // Tự động map sang/đi từ pieceType (PieceType) nội bộ.
    public ChessRaycastDebug.ChessType type
    {
        get => ToRayType(pieceType);
        set => pieceType = FromRayType(value);
    }

    // Helper cũ còn dùng ở vài nơi
    public bool IsPawn() => pieceType == PieceType.Pawn;

    public void SetPieceType(PieceType t)
    {
        pieceType = t; // 'type' sẽ phản ánh qua getter
    }

    public void MarkMoved() => hasMoved = true;

    // ======= CHUYỂN ĐỔI ENUM =======
    public static ChessRaycastDebug.ChessType ToRayType(PieceType t)
    {
        switch (t)
        {
            case PieceType.Pawn: return ChessRaycastDebug.ChessType.Pawn;
            case PieceType.Rook: return ChessRaycastDebug.ChessType.Rook;
            case PieceType.Knight: return ChessRaycastDebug.ChessType.Knight;
            case PieceType.Bishop: return ChessRaycastDebug.ChessType.Bishop;
            case PieceType.Queen: return ChessRaycastDebug.ChessType.Queen;
            case PieceType.King: return ChessRaycastDebug.ChessType.King;
            default: return ChessRaycastDebug.ChessType.Pawn;
        }
    }

    public static PieceType FromRayType(ChessRaycastDebug.ChessType t)
    {
        switch (t)
        {
            case ChessRaycastDebug.ChessType.Pawn: return PieceType.Pawn;
            case ChessRaycastDebug.ChessType.Rook: return PieceType.Rook;
            case ChessRaycastDebug.ChessType.Knight: return PieceType.Knight;
            case ChessRaycastDebug.ChessType.Bishop: return PieceType.Bishop;
            case ChessRaycastDebug.ChessType.Queen: return PieceType.Queen;
            case ChessRaycastDebug.ChessType.King: return PieceType.King;
            default: return PieceType.Pawn;
        }
    }
}
