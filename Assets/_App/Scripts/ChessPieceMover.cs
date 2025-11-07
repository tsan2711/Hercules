using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

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
        // Kiểm tra xem game đã sẵn sàng chơi chưa
        if (GameStartDelayManager.Instance != null && !GameStartDelayManager.Instance.IsGameReady)
        {
            return; // Chưa đến lúc chơi, bỏ qua input
        }
        
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
                    // Sử dụng ChessPieceController thay vì MovePiece trực tiếp
                    ChessPieceController pieceController = selectedPiece.GetComponent<ChessPieceController>();
                    if (pieceController != null)
                    {
                        // Kiểm tra xem piece có đang busy không
                        if (pieceController.IsBusy)
                        {
                            Debug.LogWarning($"Piece {selectedPiece.name} is busy, cannot move!");
                            return;
                        }
                        
                        pieceController.MovePiece(targetPos);
                    }
                    else
                    {
                        // Fallback nếu không có ChessPieceController
                        Debug.LogWarning($"No ChessPieceController found on {selectedPiece.name}, using fallback");
                        MovePiece(selectedPiece, targetPos);
                    }
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

        // Check if piece has ChessPieceController for animated movement
        ChessPieceController pieceController = piece.GetComponent<ChessPieceController>();
        
        // === Kiểm tra nhập thành ===
        if (piece.type == ChessRaycastDebug.ChessType.King && Mathf.Abs(target.x - start.x) == 2)
        {
            HandleCastling(piece, target, start, targetWorldPos);
            return;
        }

        // === Di chuyển bình thường ===
        
        // Kiểm tra có quân địch tại vị trí đích không
        ChessPieceInfo targetPiece = ChessBoardManager.Instance.board[target.x, target.y];
        bool isAttackMove = targetPiece != null && targetPiece != piece;
        
        // Sử dụng ChessPieceController nếu có
        if (pieceController != null)
        {
            pieceController.ExecuteMove(targetWorldPos, isAttackMove ? targetPiece : null, () => {
                // Callback sau khi di chuyển hoàn thành
                HandlePostMoveLogic(piece, target);
            });
        }
        else
        {
            // Fallback: Use DOTween for simple movement animation
            Vector3 startPos = piece.transform.position;
            
            // Animate movement with DOTween
            piece.transform.DOMove(targetWorldPos, 1f)
                .SetEase(Ease.OutQuart)
                .OnComplete(() => {
                    // Handle attack after movement
                    if (isAttackMove)
                    {
                        // Try to use ChessPieceController's dissolve logic
                        ChessPieceController targetController = targetPiece.GetComponent<ChessPieceController>();
                        if (targetController != null)
                        {
                            // Use reflection or public method to trigger dissolve
                            HandleTargetPieceCaptureWithDissolve(targetPiece);
                        }
                        else
                        {
                            // Fallback: use dissolve logic directly
                            HandleTargetPieceCaptureWithDissolve(targetPiece);
                        }
                    }
                    
                    // Cập nhật board
                    ChessBoardManager.Instance.UpdateBoardPosition(piece, target);
                    piece.hasMoved = true;
                    
                    HandlePostMoveLogic(piece, target);
                });
        }
    }
    
    /// <summary>
    /// Xử lý nhập thành
    /// </summary>
    private void HandleCastling(ChessPieceInfo king, Vector2Int target, Vector2Int start, Vector3 targetWorldPos)
    {
        int y = start.y;
        ChessPieceController kingController = king.GetComponent<ChessPieceController>();
        
        // Nhập thành nhỏ (king-side)
        if (target.x > start.x)
        {
            ChessPieceInfo rook = ChessBoardManager.Instance.board[7, y];
            if (rook != null && rook.type == ChessRaycastDebug.ChessType.Rook && !rook.hasMoved)
            {
                ChessPieceController rookController = rook.GetComponent<ChessPieceController>();
                Vector3 rookTargetPos = ChessBoardManager.Instance.BoardToWorld(5, y);
                
                // Di chuyển với animation nếu có controller
                if (kingController != null && rookController != null)
                {
                    // Di chuyển vua trước
                    kingController.ExecuteMove(targetWorldPos, null, () => {
                        // Sau đó di chuyển xe
                        rookController.ExecuteMove(rookTargetPos, null, () => {
                            CompleteCastling(king, rook, target, new Vector2Int(5, y), start);
                        });
                    });
                }
                else
                {
                    // Fallback: Animate both pieces with DOTween
                    Sequence castlingSequence = DOTween.Sequence();
                    castlingSequence.Append(king.transform.DOMove(targetWorldPos, 1f).SetEase(Ease.OutQuart));
                    castlingSequence.Join(rook.transform.DOMove(rookTargetPos, 1f).SetEase(Ease.OutQuart));
                    castlingSequence.OnComplete(() => {
                        CompleteCastling(king, rook, target, new Vector2Int(5, y), start);
                    });
                }
                return;
            }
        }
        // Nhập thành lớn (queen-side)
        else
        {
            ChessPieceInfo rook = ChessBoardManager.Instance.board[0, y];
            if (rook != null && rook.type == ChessRaycastDebug.ChessType.Rook && !rook.hasMoved)
            {
                ChessPieceController rookController = rook.GetComponent<ChessPieceController>();
                Vector3 rookTargetPos = ChessBoardManager.Instance.BoardToWorld(3, y);
                
                // Di chuyển với animation nếu có controller
                if (kingController != null && rookController != null)
                {
                    // Di chuyển vua trước
                    kingController.ExecuteMove(targetWorldPos, null, () => {
                        // Sau đó di chuyển xe
                        rookController.ExecuteMove(rookTargetPos, null, () => {
                            CompleteCastling(king, rook, target, new Vector2Int(3, y), start);
                        });
                    });
                }
                else
                {
                    // Fallback: Animate both pieces with DOTween
                    Sequence castlingSequence = DOTween.Sequence();
                    castlingSequence.Append(king.transform.DOMove(targetWorldPos, 1f).SetEase(Ease.OutQuart));
                    castlingSequence.Join(rook.transform.DOMove(rookTargetPos, 1f).SetEase(Ease.OutQuart));
                    castlingSequence.OnComplete(() => {
                        CompleteCastling(king, rook, target, new Vector2Int(3, y), start);
                    });
                }
                return;
            }
        }
    }
    
    /// <summary>
    /// Hoàn thành logic nhập thành
    /// </summary>
    private void CompleteCastling(ChessPieceInfo king, ChessPieceInfo rook, Vector2Int kingTarget, Vector2Int rookTarget, Vector2Int kingStart)
    {
        // Cập nhật board positions
        king.boardPosition = kingTarget;
        ChessBoardManager.Instance.board[kingStart.x, kingStart.y] = null;
        ChessBoardManager.Instance.board[kingTarget.x, kingTarget.y] = king;
        king.hasMoved = true;

        rook.boardPosition = rookTarget;
        ChessBoardManager.Instance.board[7, kingStart.y] = null; // or 0 for queenside
        ChessBoardManager.Instance.board[rookTarget.x, rookTarget.y] = rook;
        rook.hasMoved = true;
        
        // Chuyển lượt chơi sau nhập thành
        if (ChessBoardManager.Instance != null)
            ChessBoardManager.Instance.EndTurn();
    }
    
    /// <summary>
    /// Xử lý việc ăn quân với dissolve effect
    /// </summary>
    private void HandleTargetPieceCaptureWithDissolve(ChessPieceInfo targetPiece)
    {
        if (targetPiece == null) return;
        
        Debug.Log($"HandleTargetPieceCaptureWithDissolve called for {targetPiece.name} in ChessPieceMover");
        
        // Trigger explosive effect khi quân cờ bị hạ gục
        TriggerExplosiveCapture(targetPiece);
        
        // Kiểm tra PawnController trước
        PawnController pawnController = targetPiece.GetComponent<PawnController>();
        if (pawnController != null)
        {
            Debug.Log($"PawnController found, using StartDissolveOut");
            
            ChessPieceSkinController targetSkin = targetPiece.GetComponent<ChessPieceSkinController>();
            if (targetSkin != null)
            {
                targetSkin.SetSkinStateImmediate(SkinState.Dissolving);
            }
            
            pawnController.StartDissolveOut(() => {
                Debug.Log($"Destroying {targetPiece.name} after PawnController dissolve");
                CleanupAndDestroy(targetPiece);
            });
            return;
        }
        
        // Fallback: Use ChessPieceSkinController
        Debug.Log($"No PawnController found, using ChessPieceSkinController");
        ChessPieceSkinController targetSkinFallback = targetPiece.GetComponent<ChessPieceSkinController>();
        if (targetSkinFallback != null)
        {
            Debug.Log($"ChessPieceSkinController found, triggering dissolve out");
            targetSkinFallback.SetSkinStateImmediate(SkinState.Dissolving);
            targetSkinFallback.TriggerDissolveOut(() => {
                Debug.Log($"Destroying {targetPiece.name} after ChessPieceSkinController dissolve");
                CleanupAndDestroy(targetPiece);
            });
        }
        else
        {
            Debug.LogWarning($"No ChessPieceSkinController found on {targetPiece.name}, trying to add one...");
            
            // Try to add ChessPieceSkinController
            ChessPieceSkinController newSkinController = targetPiece.gameObject.AddComponent<ChessPieceSkinController>();
            if (newSkinController != null)
            {
                Debug.Log($"Added ChessPieceSkinController, triggering dissolve out");
                newSkinController.SetSkinStateImmediate(SkinState.Dissolving);
                newSkinController.TriggerDissolveOut(() => {
                    Debug.Log($"Destroying {targetPiece.name} after newly added ChessPieceSkinController dissolve");
                    CleanupAndDestroy(targetPiece);
                });
            }
            else
            {
                Debug.LogWarning($"Could not add ChessPieceSkinController, destroying immediately");
                CleanupAndDestroy(targetPiece);
            }
        }
    }
    
    /// <summary>
    /// Trigger explosive effect khi quân cờ bị hạ gục (captured)
    /// </summary>
    private void TriggerExplosiveCapture(ChessPieceInfo targetPiece)
    {
        if (targetPiece == null) return;
        
        Vector3 explosionPos = targetPiece.transform.position;
        
        Debug.Log($"TriggerExplosiveCapture called for {targetPiece.name} at {explosionPos}");
        
        // Spawn VFX explosion
        if (VFXManager.Instance != null)
        {
            // Spawn Destroy VFX cho explosive capture
            VFXManager.Instance.SpawnVFX(VFXType.Destroy, explosionPos);
            // Spawn Hit VFX để có thêm hiệu ứng
            VFXManager.Instance.SpawnVFX(VFXType.Hit, explosionPos);
        }
        
        // Apply explosion force trực tiếp lên chính quân cờ bị hạ gục
        ApplyExplosionForceToCapturedPiece(targetPiece, explosionPos);
    }
    
    /// <summary>
    /// Apply explosion animation trực tiếp lên chính quân cờ bị capture sử dụng DOTween
    /// </summary>
    private void ApplyExplosionForceToCapturedPiece(ChessPieceInfo targetPiece, Vector3 explosionPos)
    {
        if (targetPiece == null || targetPiece.gameObject == null) return;
        
        float explosionDistance = 2f; // Khoảng cách bay ra
        float explosionHeight = 1.5f; // Độ cao bay lên
        float explosionDuration = 0.8f; // Thời gian animation
        
        Debug.Log($"Applying explosion animation to {targetPiece.name} using DOTween");
        
        // Tính toán hướng nổ ngẫu nhiên để có hiệu ứng tự nhiên hơn
        Vector3 randomDirection = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        ).normalized;
        
        // Vị trí đích: bay ra xa và lên cao
        Vector3 targetExplosionPos = explosionPos + randomDirection * explosionDistance + Vector3.up * explosionHeight;
        
        // Tạo sequence animation cho hiệu ứng nổ
        Sequence explosionSequence = DOTween.Sequence();
        
        // Animation bay lên và ra xa
        explosionSequence.Append(targetPiece.transform.DOMove(targetExplosionPos, explosionDuration)
            .SetEase(Ease.OutQuad));
        
        // Animation xoay khi bay (xoay ngẫu nhiên)
        Vector3 randomRotation = new Vector3(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f)
        );
        explosionSequence.Join(targetPiece.transform.DORotate(randomRotation, explosionDuration)
            .SetEase(Ease.OutQuad));
        
        // Animation scale down nhẹ khi bay (tùy chọn)
        Vector3 originalScale = targetPiece.transform.localScale;
        explosionSequence.Join(targetPiece.transform.DOScale(originalScale * 0.9f, explosionDuration * 0.5f)
            .SetEase(Ease.OutQuad));
        
        Debug.Log($"Explosion animation started for {targetPiece.name} - moving to {targetExplosionPos}");
    }
    
    /// <summary>
    /// Cleanup board và destroy piece
    /// </summary>
    private void CleanupAndDestroy(ChessPieceInfo targetPiece)
    {
        if (targetPiece == null) return;
        
        // Cleanup board position
        if (ChessBoardManager.Instance != null)
        {
            Vector2Int boardPos = targetPiece.boardPosition;
            if (boardPos.x >= 0 && boardPos.x < 8 && boardPos.y >= 0 && boardPos.y < 8)
            {
                ChessBoardManager.Instance.board[boardPos.x, boardPos.y] = null;
            }
        }
        
        // Destroy game object
        if (targetPiece.gameObject != null)
        {
            Destroy(targetPiece.gameObject);
        }
    }
    
    /// <summary>
    /// Xử lý việc ăn quân (fallback method - deprecated, use HandleTargetPieceCaptureWithDissolve instead)
    /// </summary>
    private void HandleTargetPieceCapture(ChessPieceInfo targetPiece)
    {
        // Redirect to dissolve version
        HandleTargetPieceCaptureWithDissolve(targetPiece);
    }
    
    /// <summary>
    /// Xử lý logic sau di chuyển (pawn promotion, end turn)
    /// </summary>
    private void HandlePostMoveLogic(ChessPieceInfo piece, Vector2Int target)
    {
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
