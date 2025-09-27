using System.Collections.Generic;
using UnityEngine;

public class ChessRaycastDebug : MonoBehaviour
{
    private GameObject currentSelected;
    private ChessPieceSkinController selectedSkinController;

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
            SoundManager.Instance.PlayClick();
            if (hitSquare && selectedInfo != null) // Click vào ô highlight
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
                        
                        pieceController.MovePiece(targetPos);
                        
                        // Reset selection sau khi move thành công
                        ResetSelected();
                        ClearHighlights();
                    }
                    else
                    {
                        // Fallback nếu không có ChessPieceController
                        Debug.LogWarning($"No ChessPieceController found on {selectedInfo.name}, using fallback");
                        pieceMover.MovePiece(selectedInfo, targetPos);
                        
                        // Reset selection sau khi move thành công
                        ResetSelected();
                        ClearHighlights();
                    }
                    return;
                }
            }

            if (hitPiece) // Click vào quân cờ
            {
                GameObject pieceObj = hit.collider.gameObject;
                ChessPieceInfo pieceInfo = pieceObj.GetComponent<ChessPieceInfo>();

                if (pieceInfo != null && ChessBoardManager.Instance != null &&
                    !ChessBoardManager.Instance.CanPlayerMove(pieceInfo.isWhite))
                {
                    Debug.Log($"It's not {(pieceInfo.isWhite ? "White" : "Black")}'s turn!");
                    return;
                }

                if (pieceObj == currentSelected)
                {
                    // Đã click lại chính quân đang được chọn => không làm gì hết
                    return;
                }
                else
                {
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
                }
            }
            else
            {
                ResetSelected();
                ClearHighlights();
            }
        }

        // --- HOVER LOGIC ---
        if (hitPiece)
        {
            GameObject pieceObj = hit.collider.gameObject;

            if (pieceObj != currentHover)
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
