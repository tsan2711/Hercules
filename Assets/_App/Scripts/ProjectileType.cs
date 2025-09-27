using UnityEngine;

/// <summary>
/// Enum định nghĩa các loại projectile cho từng quân cờ
/// </summary>
[System.Serializable]
public enum ProjectileType
{
    Pawn,       // Projectile của Pawn
    Knight,     // Projectile của Knight
    Bishop,     // Projectile của Bishop
    Rook,       // Projectile của Rook
    Queen,      // Projectile của Queen
    King        // Projectile của King
}

/// <summary>
/// Static class chứa thông tin về từng loại projectile
/// </summary>
public static class ProjectileInfo
{
    /// <summary>
    /// Get projectile type từ chess piece type
    /// </summary>
    public static ProjectileType GetProjectileType(ChessRaycastDebug.ChessType chessType)
    {
        switch (chessType)
        {
            case ChessRaycastDebug.ChessType.Pawn:
                return ProjectileType.Pawn;
            case ChessRaycastDebug.ChessType.Knight:
                return ProjectileType.Knight;
            case ChessRaycastDebug.ChessType.Bishop:
                return ProjectileType.Bishop;
            case ChessRaycastDebug.ChessType.Rook:
                return ProjectileType.Rook;
            case ChessRaycastDebug.ChessType.Queen:
                return ProjectileType.Queen;
            case ChessRaycastDebug.ChessType.King:
                return ProjectileType.King;
            default:
                return ProjectileType.Pawn;
        }
    }
    
    /// <summary>
    /// Get default speed cho từng loại projectile
    /// </summary>
    public static float GetDefaultSpeed(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Pawn:
                return 3f;      // Chậm, đơn giản
            case ProjectileType.Knight:
                return 4f;      // Trung bình, có thể có arc
            case ProjectileType.Bishop:
                return 6f;      // Nhanh, thẳng
            case ProjectileType.Rook:
                return 5f;      // Trung bình, có thể có trail dài
            case ProjectileType.Queen:
                return 7f;      // Rất nhanh, mạnh mẽ
            case ProjectileType.King:
                return 4f;      // Chậm nhưng uy nghiêm
            default:
                return 5f;
        }
    }
    
    /// <summary>
    /// Get default lifetime cho từng loại projectile
    /// </summary>
    public static float GetDefaultLifetime(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Pawn:
                return 5f;      // Ngắn
            case ProjectileType.Knight:
                return 6f;      // Trung bình
            case ProjectileType.Bishop:
                return 8f;      // Dài
            case ProjectileType.Rook:
                return 7f;      // Trung bình-dài
            case ProjectileType.Queen:
                return 10f;     // Rất dài
            case ProjectileType.King:
                return 6f;      // Trung bình
            default:
                return 6f;
        }
    }
    
    /// <summary>
    /// Get collision radius cho từng loại projectile
    /// </summary>
    public static float GetCollisionRadius(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Pawn:
                return 0.3f;    // Nhỏ
            case ProjectileType.Knight:
                return 0.4f;    // Trung bình
            case ProjectileType.Bishop:
                return 0.5f;    // Trung bình
            case ProjectileType.Rook:
                return 0.6f;    // Lớn
            case ProjectileType.Queen:
                return 0.7f;    // Rất lớn
            case ProjectileType.King:
                return 0.5f;    // Trung bình
            default:
                return 0.5f;
        }
    }
    
    /// <summary>
    /// Get projectile name cho display
    /// </summary>
    public static string GetProjectileName(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Pawn:
                return "Pawn Bolt";
            case ProjectileType.Knight:
                return "Knight Strike";
            case ProjectileType.Bishop:
                return "Bishop Beam";
            case ProjectileType.Rook:
                return "Rook Cannon";
            case ProjectileType.Queen:
                return "Queen Blast";
            case ProjectileType.King:
                return "King Command";
            default:
                return "Unknown Projectile";
        }
    }
    
    /// <summary>
    /// Get projectile description
    /// </summary>
    public static string GetProjectileDescription(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Pawn:
                return "Simple energy bolt from pawn";
            case ProjectileType.Knight:
                return "Swift strike with arc trajectory";
            case ProjectileType.Bishop:
                return "Precise beam attack";
            case ProjectileType.Rook:
                return "Powerful cannon shot";
            case ProjectileType.Queen:
                return "Devastating energy blast";
            case ProjectileType.King:
                return "Royal command projectile";
            default:
                return "Unknown projectile type";
        }
    }
}
