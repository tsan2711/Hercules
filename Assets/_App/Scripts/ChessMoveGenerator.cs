using System.Collections.Generic;
using UnityEngine;

public class ChessMoveGenerator : MonoBehaviour
{
    public List<Vector3> GetMoves(ChessPieceInfo piece)
    {
        // Lấy nước đi cơ bản
        List<Vector3> basicMoves = GetBasicMoves(piece);
        
        // Nếu có ChessCheckSystem, lọc ra những nước đi hợp lệ
        if (ChessCheckSystem.Instance != null)
        {
            return ChessCheckSystem.Instance.GetLegalMoves(piece);
        }
        
        return basicMoves;
    }
    
    /// <summary>
    /// Lấy nước đi cơ bản không tính đến check (dùng nội bộ)
    /// </summary>
    public List<Vector3> GetBasicMoves(ChessPieceInfo piece)
    {
        switch (piece.type)
        {
            case ChessRaycastDebug.ChessType.Pawn: return GetPawnMoves(piece);
            case ChessRaycastDebug.ChessType.Rook: return GetRookMoves(piece);
            case ChessRaycastDebug.ChessType.Knight: return GetKnightMoves(piece);
            case ChessRaycastDebug.ChessType.Bishop: return GetBishopMoves(piece);
            case ChessRaycastDebug.ChessType.Queen: return GetQueenMoves(piece);
            case ChessRaycastDebug.ChessType.King: return GetKingMoves(piece);
        }
        return new List<Vector3>();
    }

    // =========================
    // === HELPER FUNCTIONS ====
    // =========================
    private bool HasAlly(Vector2Int pos, bool isWhite)
    {
        if (!IsInsideBoard(pos)) return false;
        ChessPieceInfo piece = ChessBoardManager.Instance.board[pos.x, pos.y];
        return piece != null && piece.isWhite == isWhite;
    }

    private bool HasEnemy(Vector2Int pos, bool isWhite)
    {
        if (!IsInsideBoard(pos)) return false;
        ChessPieceInfo piece = ChessBoardManager.Instance.board[pos.x, pos.y];
        return piece != null && piece.isWhite != isWhite;
    }

    private bool IsEmpty(Vector2Int pos)
    {
        if (!IsInsideBoard(pos)) return false;
        return ChessBoardManager.Instance.board[pos.x, pos.y] == null;
    }

    private bool IsInsideBoard(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < 8 && pos.y >= 0 && pos.y < 8;
    }

    // =========================
    // === MOVE GENERATION =====
    // =========================

    private List<Vector3> GetPawnMoves(ChessPieceInfo piece)
    {
        List<Vector3> moves = new List<Vector3>();
        int dir = piece.isWhite ? 1 : -1;
        Vector2Int pos = piece.boardPosition;

        // Đi thẳng
        Vector2Int forward = new Vector2Int(pos.x, pos.y + dir);
        if (IsInsideBoard(forward) && IsEmpty(forward))
        {
            moves.Add(BoardToWorld(forward));

            // Lần đầu được đi 2 ô
            Vector2Int doubleForward = new Vector2Int(pos.x, pos.y + dir * 2);
            bool startRow = piece.isWhite ? pos.y == 1 : pos.y == 6;
            if (startRow && IsEmpty(doubleForward))
                moves.Add(BoardToWorld(doubleForward));
        }

        // Ăn chéo trái
        Vector2Int left = new Vector2Int(pos.x - 1, pos.y + dir);
        if (HasEnemy(left, piece.isWhite))
            moves.Add(BoardToWorld(left));

        // Ăn chéo phải
        Vector2Int right = new Vector2Int(pos.x + 1, pos.y + dir);
        if (HasEnemy(right, piece.isWhite))
            moves.Add(BoardToWorld(right));

        return moves;
    }

    private List<Vector3> GetRookMoves(ChessPieceInfo piece)
    {
        List<Vector3> moves = new List<Vector3>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int current = piece.boardPosition;
            while (true)
            {
                current += dir;
                if (!IsInsideBoard(current)) break;

                if (IsEmpty(current))
                {
                    moves.Add(BoardToWorld(current));
                }
                else
                {
                    if (HasEnemy(current, piece.isWhite))
                        moves.Add(BoardToWorld(current));
                    break; // dừng khi gặp quân cờ
                }
            }
        }
        return moves;
    }

    private List<Vector3> GetBishopMoves(ChessPieceInfo piece)
    {
        List<Vector3> moves = new List<Vector3>();
        Vector2Int[] directions = {
            new Vector2Int(1,1), new Vector2Int(-1,1),
            new Vector2Int(1,-1), new Vector2Int(-1,-1)
        };
        foreach (var dir in directions)
        {
            Vector2Int current = piece.boardPosition;
            while (true)
            {
                current += dir;
                if (!IsInsideBoard(current)) break;

                if (IsEmpty(current))
                {
                    moves.Add(BoardToWorld(current));
                }
                else
                {
                    if (HasEnemy(current, piece.isWhite))
                        moves.Add(BoardToWorld(current));
                    break;
                }
            }
        }
        return moves;
    }

    private List<Vector3> GetQueenMoves(ChessPieceInfo piece)
    {
        List<Vector3> moves = new List<Vector3>();

        // 8 hướng: N, S, E, W, NE, NW, SE, SW
        Vector2Int[] directions = {
        new Vector2Int(0,1),  // N
        new Vector2Int(0,-1), // S
        new Vector2Int(1,0),  // E
        new Vector2Int(-1,0), // W
        new Vector2Int(1,1),  // NE
        new Vector2Int(-1,1), // NW
        new Vector2Int(1,-1), // SE
        new Vector2Int(-1,-1) // SW
    };

        foreach (var dir in directions)
        {
            Vector2Int current = piece.boardPosition;
            while (true)
            {
                current += dir;

                if (!IsInsideBoard(current)) break;
                if (HasAlly(current, piece.isWhite)) break;

                moves.Add(BoardToWorld(current));

                if (HasEnemy(current, piece.isWhite)) break;
            }
        }

        return moves;
    }

    private List<Vector3> GetKnightMoves(ChessPieceInfo piece)
    {
        List<Vector3> moves = new List<Vector3>();
        Vector2Int[] offsets = {
            new Vector2Int(1,2), new Vector2Int(2,1),
            new Vector2Int(-1,2), new Vector2Int(-2,1),
            new Vector2Int(1,-2), new Vector2Int(2,-1),
            new Vector2Int(-1,-2), new Vector2Int(-2,-1)
        };

        foreach (var off in offsets)
        {
            Vector2Int target = piece.boardPosition + off;
            if (!IsInsideBoard(target)) continue;
            if (HasAlly(target, piece.isWhite)) continue;

            moves.Add(BoardToWorld(target));
        }
        return moves;
    }

    private List<Vector3> GetKingMoves(ChessPieceInfo piece)
    {
        List<Vector3> moves = new List<Vector3>();
        Vector2Int start = piece.boardPosition;

        // Các nước đi thông thường
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int target = new Vector2Int(start.x + dx, start.y + dy);
                if (!IsInsideBoard(target)) continue;
                if (HasAlly(target, piece.isWhite)) continue;
                moves.Add(BoardToWorld(target));
            }
        }

        // === Nhập thành ===
        if (!piece.hasMoved)
        {
            int y = piece.isWhite ? 0 : 7; // hàng của vua
                                           // Nhập thành nhỏ (king-side)
            if (CanCastle(piece, new Vector2Int(7, y)))
            {
                moves.Add(BoardToWorld(new Vector2Int(start.x + 2, y)));
            }
            // Nhập thành lớn (queen-side)
            if (CanCastle(piece, new Vector2Int(0, y)))
            {
                moves.Add(BoardToWorld(new Vector2Int(start.x - 2, y)));
            }
        }

        return moves;
    }

    // Kiểm tra điều kiện nhập thành với một Rook
    private bool CanCastle(ChessPieceInfo king, Vector2Int rookPos)
    {
        ChessPieceInfo rook = ChessBoardManager.Instance.board[rookPos.x, rookPos.y];
        if (rook == null || rook.type != ChessRaycastDebug.ChessType.Rook || rook.isWhite != king.isWhite || rook.hasMoved)
            return false;

        int dir = rookPos.x > king.boardPosition.x ? 1 : -1;
        int startX = king.boardPosition.x + dir;
        int endX = rookPos.x - dir;

        // Kiểm tra các ô giữa vua và xe trống
        for (int x = startX; x != endX + dir; x += dir)
        {
            if (ChessBoardManager.Instance.board[x, king.boardPosition.y] != null)
                return false;
        }
        return true;
    }



    private Vector3 BoardToWorld(Vector2Int pos)
    {
        return ChessBoardManager.Instance.BoardToWorld(pos.x, pos.y);
    }
}
