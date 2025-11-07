using System.Collections.Generic;
using UnityEngine;

public class ChessRaycastDebug : MonoBehaviour
{
    private GameObject currentSelected;
    private ChessPieceSkinController selectedSkinController;
    private bool isMoving = false; // Flag để track move state

    private GameObject currentHover;
    private ChessPieceSkinController hoverSkinController;
    private Material[][] hoverOriginalMaterials; // Backup for fallback
    private Material[][] selectedOriginalMaterials; // Backup for fallback

    [Header("Square Highlight Prefabs")]
    public GameObject whiteHighlightPrefab;
    public GameObject blackHighlightPrefab;
    private List<GameObject> activeSquares = new List<GameObject>();

    [Header("Board Settings")]
    public float tileSize = 2f;

    [Header("Move Generation")]
    public ChessMoveGenerator moveGenerator;
    public ChessPieceMover pieceMover; // Thêm vào Inspector

    private ChessPieceInfo selectedInfo;
    private List<Vector3> currentMoves = new List<Vector3>();

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool hitPiece = Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("ChessPiece"));
        bool hitSquare = Physics.Raycast(ray, out RaycastHit squareHit, 100f, LayerMask.GetMask("MoveSquare"));

        // --- XỬ LÝ CLICK ---
        if (Input.GetMouseButtonDown(0))
        {
            // Kiểm tra xem có đang trong quá trình move không
            if (isMoving)
            {
                Debug.LogWarning("Cannot click while piece is moving!");
                return;
            }
            
            SoundManager.Instance.PlayClick();
            
            // Xử lý click vào quân cờ
            if (hitPiece)
            {
                GameObject pieceObj = hit.collider.gameObject;
                ChessPieceInfo pieceInfo = pieceObj.GetComponent<ChessPieceInfo>();

                // Nếu đã có quân cờ được chọn và click vào quân cờ địch đứng trên ô có thể di chuyển đến
                // => Di chuyển/tấn công đến đó
                if (selectedInfo != null && pieceInfo != null && 
                    selectedInfo.isWhite != pieceInfo.isWhite)
                {
                    // Kiểm tra xem vị trí của quân cờ địch có nằm trong danh sách nước đi hợp lệ không
                    Vector3 enemyPos = pieceObj.transform.position;
                    Vector2Int enemyBoardPos = pieceInfo.boardPosition;
                    bool isValidMove = false;
                    
                    // Kiểm tra bằng board position (chính xác hơn)
                    if (ChessBoardManager.Instance != null)
                    {
                        foreach (var movePos in currentMoves)
                        {
                            Vector2Int moveBoardPos = ChessBoardManager.Instance.WorldToBoard(movePos);
                            if (moveBoardPos == enemyBoardPos)
                            {
                                isValidMove = true;
                                break;
                            }
                        }
                    }
                    
                    // Fallback: So sánh với tolerance để tránh lỗi floating point
                    if (!isValidMove)
                    {
                        foreach (var movePos in currentMoves)
                        {
                            if (Vector3.Distance(enemyPos, movePos) < 0.1f)
                            {
                                isValidMove = true;
                                break;
                            }
                        }
                    }
                    
                    // Hoặc kiểm tra xem có hitSquare ở vị trí đó không (có ô highlight)
                    if (!isValidMove && hitSquare)
                    {
                        Vector3 squarePos = squareHit.collider.transform.position;
                        if (ChessBoardManager.Instance != null)
                        {
                            Vector2Int squareBoardPos = ChessBoardManager.Instance.WorldToBoard(squarePos);
                            if (squareBoardPos == enemyBoardPos)
                            {
                                isValidMove = true;
                            }
                        }
                        else if (Vector3.Distance(enemyPos, squarePos) < 0.1f)
                        {
                            isValidMove = true;
                        }
                    }
                    
                    if (isValidMove)
                    {
                        // Di chuyển/tấn công đến quân cờ địch
                        ChessPieceController pieceController = selectedInfo.GetComponent<ChessPieceController>();
                        if (pieceController != null)
                        {
                            if (pieceController.IsBusy)
                            {
                                Debug.LogWarning($"Piece {selectedInfo.name} is busy, cannot move!");
                                return;
                            }
                            
                            pieceController.OnActionSequenceCompleted += OnPieceMoveCompleted;
                            isMoving = true;
                            pieceController.MovePiece(enemyPos);
                            return;
                        }
                        else
                        {
                            // Fallback
                            pieceMover.MovePiece(selectedInfo, enemyPos);
                            ResetSelected();
                            ClearHighlights();
                            return;
                        }
                    }
                }

                // Kiểm tra lượt chơi
                if (pieceInfo != null && ChessBoardManager.Instance != null &&
                    !ChessBoardManager.Instance.CanPlayerMove(pieceInfo.isWhite))
                {
                    Debug.Log($"It's not {(pieceInfo.isWhite ? "White" : "Black")}'s turn!");
                    // Nếu click vào quân cờ không phải lượt chơi, vẫn cho phép hủy chọn hiện tại
                    if (currentSelected != null)
                    {
                        ResetSelected();
                        ClearHighlights();
                    }
                    return;
                }
                
                // Trong chế độ level, chỉ cho phép player (trắng) di chuyển
                if (pieceInfo != null && ChessBotAI.Instance != null && ChessBotAI.Instance.IsLevelMode())
                {
                    if (!pieceInfo.isWhite)
                    {
                        Debug.Log("[HoverChessPiece] Level mode: Only white pieces can be moved by player!");
                        // Nếu click vào quân cờ đen, vẫn cho phép hủy chọn hiện tại
                        if (currentSelected != null)
                        {
                            ResetSelected();
                            ClearHighlights();
                        }
                        return;
                    }
                }

                // Nếu click lại chính quân đang được chọn => hủy chọn
                if (pieceObj == currentSelected)
                {
                    ResetSelected();
                    ClearHighlights();
                    return;
                }
                
                // Click vào quân cờ khác => chọn quân cờ mới
                ResetSelected();
                ResetHover();

                // Use ChessPieceSkinController instead of manual material handling
                selectedSkinController = pieceObj.GetComponent<ChessPieceSkinController>();
                if (selectedSkinController != null)
                {
                    selectedSkinController.SetSkinState(SkinState.Selected);
                }
                else
                {
                    // Fallback to old method if no skin controller
                    Debug.LogWarning($"No ChessPieceSkinController found on {pieceObj.name}, using fallback");
                    ApplyHighlight(pieceObj, ref selectedOriginalMaterials);
                }
                currentSelected = pieceObj;

                selectedInfo = pieceObj.GetComponent<ChessPieceInfo>();
                if (selectedInfo != null)
                {
                    // Sử dụng ChessCheckSystem để chỉ lấy những nước đi hợp lệ
                    if (ChessCheckSystem.Instance != null)
                    {
                        currentMoves = ChessCheckSystem.Instance.GetLegalMoves(selectedInfo);
                    }
                    else
                    {
                        // Fallback nếu ChessCheckSystem chưa có
                        currentMoves = ChessCheckSystem.Instance.GetLegalMoves(selectedInfo);
                    }
                    
                    ClearHighlights();
                    GameObject prefabToUse = selectedInfo.isWhite ? whiteHighlightPrefab : blackHighlightPrefab;

                    // Chỉ tạo highlight cho những nước đi hợp lệ
                    foreach (var pos in currentMoves)
                    {
                        GameObject obj = Instantiate(prefabToUse, pos, Quaternion.identity);
                        activeSquares.Add(obj);
                    }
                    
                    // Debug log
                    Debug.Log($"[Legal Moves] {selectedInfo.type} {(selectedInfo.isWhite ? "Trắng" : "Đen")} có {currentMoves.Count} nước đi hợp lệ");
                }
                return; // Return ngay sau khi xử lý click vào quân cờ
            }
            
            // Xử lý click vào ô highlight để di chuyển (chỉ khi không click vào quân cờ)
            if (hitSquare && selectedInfo != null)
            {
                Vector3 targetPos = squareHit.collider.transform.position;
                if (currentMoves.Contains(targetPos))
                {
                    // Sử dụng ChessPieceController thay vì ChessPieceMover
                    ChessPieceController pieceController = selectedInfo.GetComponent<ChessPieceController>();
                    if (pieceController != null)
                    {
                        // Kiểm tra xem piece có đang busy không
                        if (pieceController.IsBusy)
                        {
                            Debug.LogWarning($"Piece {selectedInfo.name} is busy, cannot move!");
                            return;
                        }
                        
                        // Subscribe to move completion event
                        pieceController.OnActionSequenceCompleted += OnPieceMoveCompleted;
                        
                        // Set moving flag
                        isMoving = true;
                        
                        pieceController.MovePiece(targetPos);
                        
                        // DON'T reset selection here - wait for completion event
                    }
                    else
                    {
                        // Fallback nếu không có ChessPieceController
                        Debug.LogWarning($"No ChessPieceController found on {selectedInfo.name}, using fallback");
                        pieceMover.MovePiece(selectedInfo, targetPos);
                        
                        // Reset selection ngay lập tức cho fallback
                        ResetSelected();
                        ClearHighlights();
                    }
                    return;
                }
            }
            
            // Click vào chỗ trống (không phải quân cờ, không phải ô highlight) => hủy chọn
            ResetSelected();
            ClearHighlights();
        }

        // --- HOVER LOGIC ---
        if (hitPiece)
        {
            GameObject pieceObj = hit.collider.gameObject;
            ChessPieceInfo pieceInfo = pieceObj.GetComponent<ChessPieceInfo>();

            // Kiểm tra xem có nên hiển thị hover effect không
            bool shouldShowHover = false;
            
            if (pieceInfo != null && ChessBoardManager.Instance != null)
            {
                // Chỉ hiển thị hover cho quân cờ của lượt hiện tại
                bool isCurrentTurn = ChessBoardManager.Instance.CanPlayerMove(pieceInfo.isWhite);
                
                // Trong chế độ level, không hiển thị hover cho quân bot (đen)
                bool isLevelModeBot = false;
                if (ChessBotAI.Instance != null && ChessBotAI.Instance.IsLevelMode())
                {
                    isLevelModeBot = !pieceInfo.isWhite; // Quân đen trong chế độ level
                }
                
                shouldShowHover = isCurrentTurn && !isLevelModeBot;
            }

            if (shouldShowHover && pieceObj != currentHover)
            {
                SoundManager.Instance.PlayHover();

                ResetHover();
                
                // Use ChessPieceSkinController for hover effect
                hoverSkinController = pieceObj.GetComponent<ChessPieceSkinController>();
                if (hoverSkinController != null && hoverSkinController.CurrentState == SkinState.Normal)
                {
                    hoverSkinController.SetSkinState(SkinState.Hover);
                }
                else if (hoverSkinController == null)
                {
                    // Fallback to old method
                    ApplyHighlight(pieceObj, ref hoverOriginalMaterials);
                }
                
                currentHover = pieceObj;
            }
            else if (!shouldShowHover)
            {
                // Nếu không nên hiển thị hover, reset hover hiện tại
                if (currentHover == pieceObj)
                {
                    ResetHover();
                    currentHover = null;
                }
            }
        }
        else
        {
            ResetHover();
            currentHover = null;
        }
    }

    void ClearHighlights()
    {
        foreach (var sq in activeSquares)
            Destroy(sq);
        activeSquares.Clear();
    }

    public enum ChessType { Pawn, Bishop, Rook, Knight, Queen, King }

    [System.Serializable]
    public class ChessMaterial
    {
        public bool isWhite;
        public ChessType type;
        public Material[] materials;
    }

    public ChessMaterial[] chessMaterials;

    Material[] GetMaterialForPiece(bool isWhite, ChessType type)
    {
        foreach (var cm in chessMaterials)
            if (cm.isWhite == isWhite && cm.type == type)
                return cm.materials;
        return null;
    }

    void ApplyHighlight(GameObject piece, ref Material[][] originalMaterialsArray)
    {
        SkinnedMeshRenderer[] renderers = piece.GetComponentsInChildren<SkinnedMeshRenderer>();
        originalMaterialsArray = new Material[renderers.Length][];

        ChessPieceInfo info = piece.GetComponent<ChessPieceInfo>();
        if (info == null) return;

        Material[] mats = GetMaterialForPiece(info.isWhite, info.type);

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterialsArray[i] = renderers[i].sharedMaterials;
            Material[] newMats = new Material[renderers[i].sharedMaterials.Length];
            for (int j = 0; j < newMats.Length; j++)
                newMats[j] = mats[Mathf.Min(j, mats.Length - 1)];
            renderers[i].sharedMaterials = newMats;
        }
    }

    void ResetHover()
    {
        if (currentHover == null) return;
        
        // Use ChessPieceSkinController to reset hover state
        if (hoverSkinController != null)
        {
            hoverSkinController.SetSkinState(SkinState.Normal);
            hoverSkinController = null;
        }
        else
        {
            // Fallback to old method
            SkinnedMeshRenderer[] renderers = currentHover.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterials = hoverOriginalMaterials[i];
        }
        
        currentHover = null;
    }

    /// <summary>
    /// Callback khi piece hoàn thành di chuyển
    /// </summary>
    private void OnPieceMoveCompleted(ChessPieceController pieceController)
    {
        // Unsubscribe from event
        pieceController.OnActionSequenceCompleted -= OnPieceMoveCompleted;
        
        // Reset moving flag
        isMoving = false;
        
        // Reset selection sau khi move hoàn thành
        ResetSelected();
        ClearHighlights();
        
        Debug.Log($"Piece {pieceController.name} completed move sequence");
    }

    void ResetSelected()
    {
        if (currentSelected == null) return;
        
        // Use ChessPieceSkinController to reset selected state
        if (selectedSkinController != null)
        {
            selectedSkinController.SetSkinState(SkinState.Normal);
            selectedSkinController = null;
        }
        else
        {
            // Fallback to old method
            SkinnedMeshRenderer[] renderers = currentSelected.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterials = selectedOriginalMaterials[i];
        }
        
        currentSelected = null;
        selectedInfo = null;
    }
}
