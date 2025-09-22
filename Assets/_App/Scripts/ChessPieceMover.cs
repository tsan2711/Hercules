using System.Collections.Generic;
using UnityEngine;

public class ChessPieceMover : MonoBehaviour
{
    public LayerMask squareLayer; // Layer của các ô highlight
    private ChessPieceInfo selectedPiece;
    private List<Vector3> currentMoves = new List<Vector3>();

    // Gọi từ script highlight khi chọn quân
    public void SelectPiece(ChessPieceInfo piece, List<Vector3> moves)
    {
        selectedPiece = piece;
        currentMoves = moves;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    private void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // Nếu click vào ô highlight
            if (hit.collider.CompareTag("MoveSquare"))
            {
                Vector3 targetPos = hit.collider.transform.position;
                if (selectedPiece != null && currentMoves.Contains(targetPos))
                {
                    MovePiece(selectedPiece, targetPos);
                    selectedPiece = null;
                    currentMoves.Clear();
                    Debug.Log("Moved piece to " + targetPos);
                }
            }
        }
    }

    public void MovePiece(ChessPieceInfo piece, Vector3 targetWorldPos)
    {
        Vector2Int start = piece.boardPosition;
        Vector2Int target = ChessBoardManager.Instance.WorldToBoard(targetWorldPos);

        // === Kiểm tra nhập thành ===
        if (piece.type == ChessRaycastDebug.ChessType.King && Mathf.Abs(target.x - start.x) == 2)
        {
            int y = start.y;

            // Nhập thành nhỏ (king-side)
            if (target.x > start.x)
            {
                ChessPieceInfo rook = ChessBoardManager.Instance.board[7, y];
                if (rook != null && rook.type == ChessRaycastDebug.ChessType.Rook && !rook.hasMoved)
                {
                    // Di chuyển vua
                    piece.transform.position = targetWorldPos;
                    piece.boardPosition = target;
                    ChessBoardManager.Instance.board[start.x, y] = null;
                    ChessBoardManager.Instance.board[target.x, y] = piece;
                    piece.hasMoved = true;

                    // Di chuyển xe
                    Vector2Int rookTarget = new Vector2Int(5, y);
                    rook.transform.position = ChessBoardManager.Instance.BoardToWorld(rookTarget.x, rookTarget.y);
                    rook.boardPosition = rookTarget;
                    ChessBoardManager.Instance.board[7, y] = null;
                    ChessBoardManager.Instance.board[5, y] = rook;
                    rook.hasMoved = true;
                    
                    // Chuyển lượt chơi sau nhập thành
                    if (ChessBoardManager.Instance != null)
                        ChessBoardManager.Instance.EndTurn();
                    return;
                }
            }
            // Nhập thành lớn (queen-side)
            else
            {
                ChessPieceInfo rook = ChessBoardManager.Instance.board[0, y];
                if (rook != null && rook.type == ChessRaycastDebug.ChessType.Rook && !rook.hasMoved)
                {
                    // Di chuyển vua
                    piece.transform.position = targetWorldPos;
                    piece.boardPosition = target;
                    ChessBoardManager.Instance.board[start.x, y] = null;
                    ChessBoardManager.Instance.board[target.x, y] = piece;
                    piece.hasMoved = true;

                    // Di chuyển xe
                    Vector2Int rookTarget = new Vector2Int(3, y);
                    rook.transform.position = ChessBoardManager.Instance.BoardToWorld(rookTarget.x, rookTarget.y);
                    rook.boardPosition = rookTarget;
                    ChessBoardManager.Instance.board[0, y] = null;
                    ChessBoardManager.Instance.board[3, y] = rook;
                    rook.hasMoved = true;
                    
                    // Chuyển lượt chơi sau nhập thành
                    if (ChessBoardManager.Instance != null)
                        ChessBoardManager.Instance.EndTurn();
                    return;
                }
            }
        }

        // === Di chuyển bình thường ===
        piece.transform.position = targetWorldPos;

        // Nếu ăn quân địch
        ChessPieceInfo targetPiece = ChessBoardManager.Instance.board[target.x, target.y];
        if (targetPiece != null && targetPiece != piece)
        {
            Destroy(targetPiece.gameObject); // Hoặc xử lý khác nếu muốn
        }

        // Cập nhật board
        ChessBoardManager.Instance.UpdateBoardPosition(piece, target);
        piece.hasMoved = true;
        
        // Kiểm tra phong cấp tốt
        if (piece.type == ChessRaycastDebug.ChessType.Pawn)
        {
            CheckPawnPromotion(piece, target);
        }
        
        // Chuyển lượt chơi
        if (ChessBoardManager.Instance != null)
            ChessBoardManager.Instance.EndTurn();
    }
    
    // Kiểm tra và thực hiện phong cấp tốt
    private void CheckPawnPromotion(ChessPieceInfo pawn, Vector2Int targetPos)
    {
        // Kiểm tra tốt có đến cuối bàn cờ không
        bool reachedEnd = (pawn.isWhite && targetPos.y == 7) || (!pawn.isWhite && targetPos.y == 0);
        
        if (reachedEnd)
        {
            Debug.Log($"Pawn promotion! {(pawn.isWhite ? "White" : "Black")} pawn reached the end!");
            
            // Phong cấp thành Hậu (Queen)
            PromotePawn(pawn, ChessRaycastDebug.ChessType.Queen);
        }
    }
    
    // Thực hiện phong cấp tốt
    private void PromotePawn(ChessPieceInfo pawn, ChessRaycastDebug.ChessType newType)
    {
        Vector2Int pos = pawn.boardPosition;
        bool isWhite = pawn.isWhite;
        
        // Xóa tốt cũ
        Destroy(pawn.gameObject);
        ChessBoardManager.Instance.board[pos.x, pos.y] = null;
        
        // Tạo quân mới
        GameObject newPiecePrefab = GetPromotionPrefab(isWhite, newType);
        if (newPiecePrefab != null)
        {
            Vector3 worldPos = ChessBoardManager.Instance.BoardToWorld(pos.x, pos.y);
            
            // Logic xoay cho quân phong cấp: chỉ quân trắng xoay 180 độ, quân đen giữ nguyên
            Quaternion rot = isWhite ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            
            GameObject newPieceObj = Instantiate(newPiecePrefab, worldPos, rot);
            
            // Đảm bảo quân phong cấp có đúng hướng
            newPieceObj.transform.rotation = rot;
            
            Debug.Log($"Promoted piece rotation set to: {rot.eulerAngles} for {(isWhite ? "White" : "Black")} {newType}");
            
            ChessPieceInfo newPieceInfo = newPieceObj.GetComponent<ChessPieceInfo>();
            newPieceInfo.isWhite = isWhite;
            newPieceInfo.type = newType;
            newPieceInfo.boardPosition = pos;
            newPieceInfo.hasMoved = true; // Quân phong cấp đã được coi là đã di chuyển
            
            ChessBoardManager.Instance.board[pos.x, pos.y] = newPieceInfo;
            
            Debug.Log($"Pawn promoted to {newType}!");
        }
    }
    
    // Lấy prefab cho quân phong cấp
    private GameObject GetPromotionPrefab(bool isWhite, ChessRaycastDebug.ChessType type)
    {
        ChessBoardManager boardManager = ChessBoardManager.Instance;
        
        if (isWhite)
        {
            switch (type)
            {
                case ChessRaycastDebug.ChessType.Queen: return boardManager.whiteQueenPrefab;
                case ChessRaycastDebug.ChessType.Rook: return boardManager.whiteRookPrefab;
                case ChessRaycastDebug.ChessType.Bishop: return boardManager.whiteBishopPrefab;
                case ChessRaycastDebug.ChessType.Knight: return boardManager.whiteKnightPrefab;
            }
        }
        else
        {
            switch (type)
            {
                case ChessRaycastDebug.ChessType.Queen: return boardManager.blackQueenPrefab;
                case ChessRaycastDebug.ChessType.Rook: return boardManager.blackRookPrefab;
                case ChessRaycastDebug.ChessType.Bishop: return boardManager.blackBishopPrefab;
                case ChessRaycastDebug.ChessType.Knight: return boardManager.blackKnightPrefab;
            }
        }
        
        return null;
    }

}
