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
    }

}
