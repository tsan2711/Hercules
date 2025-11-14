using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

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

    [Header("Piece Name Tooltip")]
    public TextMeshProUGUI pieceNameText; // Text để hiển thị tên quân cờ
    public Canvas tooltipCanvas; // Canvas chứa tooltip (nếu cần)
    [Tooltip("Offset Y để hiển thị tên quân cờ phía trên quân cờ")]
    public float tooltipYOffset = 3f;
    [Tooltip("Font của Dark UI để hiển thị tên quân cờ (TMP_FontAsset)")]
    public TMP_FontAsset darkUIFont; // Font của Dark UI

    [Header("Camera Zoom Settings")]
    [Tooltip("Camera để zoom (nếu null sẽ tự động tìm Main Camera)")]
    public Camera targetCamera;
    [Tooltip("Khoảng cách zoom về phía quân cờ (tăng giá trị = zoom gần hơn)")]
    public float zoomDistance = 5f; // Zoom gần hơn
    [Tooltip("Thời gian animation zoom (giây)")]
    public float zoomDuration = 0.3f;
    [Tooltip("Có bật zoom nhẹ khi chọn quân cờ không")]
    public bool enableCameraZoom = true;

    private ChessPieceInfo selectedInfo;
    private List<Vector3> currentMoves = new List<Vector3>();
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private bool isZooming = false;
    private Tween cameraZoomTween;

    void Awake()
    {
        // Tự động tìm font Dark UI nếu chưa được gán
        if (darkUIFont == null)
        {
            LoadDarkUIFont();
        }
        
        // Tự động tìm camera nếu chưa được gán
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        
        // Lưu vị trí và rotation ban đầu của camera
        if (targetCamera != null)
        {
            originalCameraPosition = targetCamera.transform.position;
            originalCameraRotation = targetCamera.transform.rotation;
        }
    }

    /// <summary>
    /// Tự động load font Dark UI
    /// </summary>
    private void LoadDarkUIFont()
    {
        #if UNITY_EDITOR
        // Trong Editor, sử dụng AssetDatabase để tìm font
        string[] fontPaths = {
            "Assets/Dark UI/Fonts/Larke Sans Regular SDF",
            "Assets/Dark UI/Fonts/Roboto-Regular SDF",
            "Assets/Dark UI/Fonts/Larke Sans Bold SDF"
        };
        
        foreach (string path in fontPaths)
        {
            TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
            {
                darkUIFont = font;
                Debug.Log($"[HoverChessPiece] Đã tự động load font Dark UI: {path}");
                return;
            }
        }
        #else
        // Trong build, thử load từ Resources
        darkUIFont = Resources.Load<TMP_FontAsset>("Larke Sans Regular SDF");
        if (darkUIFont == null)
        {
            darkUIFont = Resources.Load<TMP_FontAsset>("Roboto-Regular SDF");
        }
        #endif
        
        if (darkUIFont == null)
        {
            Debug.LogWarning("[HoverChessPiece] Không tìm thấy font Dark UI. Vui lòng gán font thủ công trong Inspector.");
        }
    }

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool hitPiece = Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("ChessPiece"));
        bool hitSquare = Physics.Raycast(ray, out RaycastHit squareHit, 100f, LayerMask.GetMask("MoveSquare"));

        // --- XỬ LÝ CLICK ---
        if (Input.GetMouseButtonDown(0))
        {
            // Kiểm tra xem game đã sẵn sàng chơi chưa
            if (GameStartDelayManager.Instance != null && !GameStartDelayManager.Instance.IsGameReady)
            {
                return; // Chưa đến lúc chơi, bỏ qua input
            }
            
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
                            
                            // Tắt tất cả glow khi bắt đầu di chuyển
                            ResetHover(); // Đảm bảo không có hover glow
                            if (selectedSkinController != null)
                            {
                                selectedSkinController.SetSkinState(SkinState.Normal);
                            }
                            
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
                
                // Zoom camera nhẹ về phía quân cờ được chọn
                if (enableCameraZoom && selectedInfo != null)
                {
                    ZoomCameraToPiece(pieceObj.transform);
                }
                
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

                    // Lấy độ cao Y từ boardOrigin để đảm bảo tất cả highlight đều ở cùng độ cao (sát mặt đất)
                    float highlightY = 2.0f; // Mặc định
                    if (ChessBoardManager.Instance != null)
                    {
                        highlightY = ChessBoardManager.Instance.boardOrigin.y; // Sát mặt đất, không có offset
                    }

                    // Chỉ tạo highlight cho những nước đi hợp lệ
                    int highlightCount = 0;
                    foreach (var pos in currentMoves)
                    {
                        // Điều chỉnh vị trí Y để đảm bảo highlight luôn hiển thị trên mặt bàn cờ
                        // Giữ nguyên X và Z, chỉ thay đổi Y
                        Vector3 highlightPos = new Vector3(pos.x, highlightY, pos.z);
                        
                        GameObject obj = Instantiate(prefabToUse, highlightPos, Quaternion.identity);
                        if (obj != null)
                        {
                            activeSquares.Add(obj);
                            highlightCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"[HoverChessPiece] Failed to instantiate highlight at position {highlightPos}");
                        }
                    }
                    
                    // Debug log
                    Debug.Log($"[Legal Moves] {selectedInfo.type} {(selectedInfo.isWhite ? "Trắng" : "Đen")} có {currentMoves.Count} nước đi hợp lệ, đã tạo {highlightCount} highlight squares tại độ cao Y={highlightY}");
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
                        
                        // Tắt tất cả glow khi bắt đầu di chuyển
                        ResetHover(); // Đảm bảo không có hover glow
                        if (selectedSkinController != null)
                        {
                            selectedSkinController.SetSkinState(SkinState.Normal);
                        }
                        
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
        // Kiểm tra xem game đã sẵn sàng chơi chưa
        bool canInteract = GameStartDelayManager.Instance == null || GameStartDelayManager.Instance.IsGameReady;
        
        // Hiển thị hover effect khi:
        // 1. Game đã sẵn sàng
        // 2. Hover vào chess piece của team mình
        // 3. Không đang select piece nào (currentSelected == null)
        // 4. Không đang di chuyển (isMoving == false)
        if (hitPiece && canInteract)
        {
            GameObject pieceObj = hit.collider.gameObject;
            ChessPieceInfo pieceInfo = pieceObj.GetComponent<ChessPieceInfo>();
            
            // Chỉ hover vào quân cờ của team mình và khi không đang select/moving
            bool canHover = pieceInfo != null &&
                            ChessBoardManager.Instance != null &&
                            ChessBoardManager.Instance.CanPlayerMove(pieceInfo.isWhite) &&
                            currentSelected == null && 
                            !isMoving;
            
            // Trong chế độ level, chỉ cho hover quân trắng (player)
            if (canHover && ChessBotAI.Instance != null && ChessBotAI.Instance.IsLevelMode())
            {
                canHover = pieceInfo.isWhite;
            }
            
            if (canHover && pieceObj != currentHover)
            {
                // Reset hover cũ nếu có
                ResetHover();
                
                // Apply hover effect cho piece mới
                currentHover = pieceObj;
                hoverSkinController = pieceObj.GetComponent<ChessPieceSkinController>();
                
                if (hoverSkinController != null)
                {
                    hoverSkinController.SetSkinState(SkinState.Hover);
                }
                else
                {
                    // Fallback to old method if no skin controller
                    ApplyHighlight(pieceObj, ref hoverOriginalMaterials);
                }
                
                // Hiển thị tên quân cờ
                ShowPieceName(pieceInfo);
            }
            else if (canHover && pieceObj == currentHover)
            {
                // Cập nhật vị trí text khi đang hover (để text luôn theo dõi quân cờ)
                UpdatePieceNamePosition();
            }
            else if (!canHover && currentHover != null)
            {
                // Nếu không thể hover (đang select/moving) thì reset hover
                ResetHover();
                currentHover = null;
                HidePieceName();
            }
        }
        else
        {
            // Không hover vào gì cả => reset hover
            ResetHover();
            currentHover = null;
            HidePieceName();
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
        HidePieceName();
    }
    
    /// <summary>
    /// Hiển thị tên quân cờ khi hover
    /// </summary>
    private void ShowPieceName(ChessPieceInfo pieceInfo)
    {
        if (pieceNameText == null || pieceInfo == null) return;
        
        // Set font của Dark UI nếu có
        if (darkUIFont != null)
        {
            pieceNameText.font = darkUIFont;
        }
        
        // Lấy tên quân cờ bằng tiếng Anh
        string pieceName = GetPieceNameEnglish(pieceInfo.type, pieceInfo.isWhite);
        
        // Hiển thị text
        pieceNameText.text = pieceName;
        pieceNameText.gameObject.SetActive(true);
        
        // Cập nhật vị trí text theo vị trí quân cờ (nếu dùng World Space Canvas)
        if (currentHover != null)
        {
            Vector3 piecePos = currentHover.transform.position;
            Vector3 textPos = new Vector3(piecePos.x, piecePos.y + tooltipYOffset, piecePos.z);
            
            // Nếu text là World Space Canvas
            if (pieceNameText.canvas != null && pieceNameText.canvas.renderMode == RenderMode.WorldSpace)
            {
                pieceNameText.transform.position = textPos;
            }
            // Nếu text là Screen Space Canvas, chuyển world position sang screen position
            else
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(textPos);
                pieceNameText.transform.position = screenPos;
            }
        }
    }
    
    /// <summary>
    /// Ẩn tên quân cờ
    /// </summary>
    private void HidePieceName()
    {
        if (pieceNameText != null)
        {
            pieceNameText.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Cập nhật vị trí text theo vị trí quân cờ (gọi mỗi frame khi đang hover)
    /// </summary>
    private void UpdatePieceNamePosition()
    {
        if (pieceNameText == null || !pieceNameText.gameObject.activeSelf || currentHover == null) return;
        
        Vector3 piecePos = currentHover.transform.position;
        Vector3 textPos = new Vector3(piecePos.x, piecePos.y + tooltipYOffset, piecePos.z);
        
        // Nếu text là World Space Canvas
        if (pieceNameText.canvas != null && pieceNameText.canvas.renderMode == RenderMode.WorldSpace)
        {
            pieceNameText.transform.position = textPos;
        }
        // Nếu text là Screen Space Canvas, chuyển world position sang screen position
        else
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(textPos);
            pieceNameText.transform.position = screenPos;
        }
    }
    
    /// <summary>
    /// Lấy tên quân cờ bằng tiếng Anh
    /// </summary>
    private string GetPieceNameEnglish(ChessType type, bool isWhite)
    {
        string teamName = isWhite ? "White" : "Black";
        string pieceName = "";
        
        switch (type)
        {
            case ChessType.Pawn:
                pieceName = "Pawn";
                break;
            case ChessType.Rook:
                pieceName = "Rook";
                break;
            case ChessType.Knight:
                pieceName = "Knight";
                break;
            case ChessType.Bishop:
                pieceName = "Bishop";
                break;
            case ChessType.Queen:
                pieceName = "Queen";
                break;
            case ChessType.King:
                pieceName = "King";
                break;
        }
        
        return $"{pieceName} ({teamName})";
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
        
        // Reset selection sau khi move hoàn thành (tắt glow)
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
        
        // Reset camera về vị trí ban đầu
        if (enableCameraZoom)
        {
            ResetCameraZoom();
        }
    }
    
    /// <summary>
    /// Zoom camera nhẹ về phía quân cờ được chọn
    /// </summary>
    private void ZoomCameraToPiece(Transform pieceTransform)
    {
        if (targetCamera == null || pieceTransform == null) return;
        
        // Kill tween cũ nếu có
        if (cameraZoomTween != null && cameraZoomTween.IsActive())
        {
            cameraZoomTween.Kill();
        }
        
        // Lưu vị trí ban đầu nếu chưa zoom
        if (!isZooming)
        {
            originalCameraPosition = targetCamera.transform.position;
            originalCameraRotation = targetCamera.transform.rotation;
        }
        
        isZooming = true;
        
        // Tính toán vị trí camera mới: di chuyển nhẹ về phía quân cờ
        Vector3 piecePosition = pieceTransform.position;
        Vector3 cameraToPiece = piecePosition - targetCamera.transform.position;
        float currentDistance = cameraToPiece.magnitude;
        
        // Tính toán vị trí mới: di chuyển camera gần hơn một khoảng zoomDistance
        // Nhưng không quá gần (giữ khoảng cách tối thiểu 50% khoảng cách ban đầu)
        float newDistance = Mathf.Max(currentDistance - zoomDistance, currentDistance * 0.5f);
        Vector3 direction = cameraToPiece.normalized;
        Vector3 targetCameraPos = piecePosition - direction * newDistance;
        
        // Giữ nguyên chiều cao Y của camera (chỉ di chuyển theo X và Z)
        targetCameraPos.y = targetCamera.transform.position.y;
        
        // Giữ nguyên rotation của camera (chỉ di chuyển, không xoay)
        Quaternion targetRotation = targetCamera.transform.rotation;
        
        // Animate camera position (mượt mà, nhẹ nhàng)
        cameraZoomTween = targetCamera.transform.DOMove(targetCameraPos, zoomDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                isZooming = false;
            });
    }
    
    /// <summary>
    /// Reset camera về vị trí ban đầu
    /// </summary>
    private void ResetCameraZoom()
    {
        if (targetCamera == null) return;
        
        // Kill tween cũ nếu có
        if (cameraZoomTween != null && cameraZoomTween.IsActive())
        {
            cameraZoomTween.Kill();
        }
        
        if (isZooming || originalCameraPosition != Vector3.zero)
        {
            // Animate camera về vị trí ban đầu (mượt mà)
            cameraZoomTween = targetCamera.transform.DOMove(originalCameraPosition, zoomDuration)
                .SetEase(Ease.InOutQuad)
                .OnComplete(() => {
                    isZooming = false;
                });
        }
    }
    
    void OnDestroy()
    {
        // Kill tween khi destroy
        if (cameraZoomTween != null && cameraZoomTween.IsActive())
        {
            cameraZoomTween.Kill();
        }
    }
}
