using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ChessRaycastDebug : MonoBehaviour
{
    private GameObject currentSelected;
    private ChessPieceSkinController selectedSkinController;
    private bool isMoving = false;

    private GameObject currentHover;
    private ChessPieceSkinController hoverSkinController;
    private Material[][] hoverOriginalMaterials;    // fallback
    private Material[][] selectedOriginalMaterials; // fallback

    [Header("Square Highlight Prefabs")]
    public GameObject whiteHighlightPrefab;
    public GameObject blackHighlightPrefab;
    private readonly List<GameObject> activeSquares = new List<GameObject>();

    [Header("Move Generation")]
    public ChessMoveGenerator moveGenerator; // fallback nếu không có ChessCheckSystem
    public ChessPieceMover pieceMover;       // fallback nếu không có ChessPieceController

    private ChessPieceInfo selectedInfo;

    // Danh sách nước đi theo ô
    private List<Vector2Int> currentMoves = new List<Vector2Int>();

    // Debug hover/click logs
    private Vector2Int? _lastHoverCell = null;
    private float _lastLogTime = 0f;

    void Update()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool hitPiece = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("ChessPiece"));

        // ======================= CLICK =======================
        if (Input.GetMouseButtonDown(0))
        {
            if (isMoving)
            {
                Debug.LogWarning("Cannot click while piece is moving!");
                return;
            }

            SoundManager.Instance?.PlayClick();

            // Nếu đã chọn quân: click đâu cũng quy về ô (plane-pick)
            if (selectedInfo != null && TryGetMouseCell(out var clickedCell, out var clickWorld))
            {
                var snap = ChessBoardManager.Instance.BoardToWorld(clickedCell.x, clickedCell.y);
                Debug.Log($"[CLICK] world={clickWorld:F3} -> cell=({clickedCell.x},{clickedCell.y}) -> snap={snap:F3}");

                if (currentMoves.Contains(clickedCell))
                {
                    Vector3 targetWorld = ChessBoardManager.Instance.BoardToWorld(clickedCell.x, clickedCell.y);

                    var pieceController = selectedInfo.GetComponent<ChessPieceController>();
                    if (pieceController != null)
                    {
                        if (pieceController.IsBusy)
                        {
                            Debug.LogWarning($"Piece {selectedInfo.name} is busy, cannot move!");
                            return;
                        }

                        pieceController.OnActionSequenceCompleted += OnPieceMoveCompleted;
                        isMoving = true;
                        pieceController.MovePiece(targetWorld);
                        // Nếu UpdateBoardPosition không được gọi ở Controller:
                        // ChessBoardManager.Instance.UpdateBoardPosition(selectedInfo, clickedCell);
                    }
                    else
                    {
                        Debug.LogWarning($"No ChessPieceController on {selectedInfo.name}, using fallback mover");
                        pieceMover?.MovePiece(selectedInfo, targetWorld);
                        // ChessBoardManager.Instance.UpdateBoardPosition(selectedInfo, clickedCell);
                        ResetSelected();
                        ClearHighlights();
                    }
                    return;
                }
            }

            // =================== CHỌN QUÂN ====================
            if (hitPiece)
            {
                GameObject pieceObj = hit.collider.gameObject;
                var pieceInfo = pieceObj.GetComponent<ChessPieceInfo>();

                if (pieceInfo != null && ChessBoardManager.Instance != null &&
                    !ChessBoardManager.Instance.CanPlayerMove(pieceInfo.isWhite))
                {
                    Debug.Log($"It's not {(pieceInfo.isWhite ? "White" : "Black")}'s turn!");
                    return;
                }

                if (pieceObj == currentSelected) return;

                ResetSelected();
                ResetHover();

                // Skin select
                selectedSkinController = pieceObj.GetComponent<ChessPieceSkinController>();
                if (selectedSkinController != null) selectedSkinController.SetSkinState(SkinState.Selected);
                else
                {
                    Debug.LogWarning($"No ChessPieceSkinController on {pieceObj.name}, using material fallback");
                    ApplyHighlight(pieceObj, ref selectedOriginalMaterials);
                }

                currentSelected = pieceObj;
                selectedInfo = pieceObj.GetComponent<ChessPieceInfo>();

                // Log ô của quân
                if (selectedInfo != null && ChessBoardManager.Instance != null)
                {
                    var posW = currentSelected.transform.position;
                    var posC = ChessBoardManager.Instance.WorldToBoard(posW);
                    Debug.Log($"[SELECT] {selectedInfo.type} {(selectedInfo.isWhite ? "White" : "Black")} at cell=({posC.x},{posC.y}) world={posW:F3}");
                }

                // ======= Lấy nước đi hợp lệ (ô) =======
                currentMoves.Clear();

                if (selectedInfo != null)
                {
                    bool gotMoves = false;

                    // Ưu tiên hệ thống luật trung tâm (nếu có)
                    if (ChessCheckSystem.Instance != null)
                    {
                        var movesWorld = ChessCheckSystem.Instance.GetLegalMoves(selectedInfo); // thường là List<Vector3>
                        ConvertWorldListToCells(movesWorld, currentMoves);
                        gotMoves = true;
                    }
                    else
                    {
                        // Fallback: thử moveGenerator với nhiều tên hàm (reflection)
                        if (TryGetMovesFromGenerator(selectedInfo, out var cells))
                        {
                            currentMoves = cells;
                            gotMoves = true;
                        }
                    }

                    if (!gotMoves)
                    {
                        Debug.LogWarning("No move provider found: ChessCheckSystem & ChessMoveGenerator don't expose a compatible API.");
                        currentMoves.Clear();
                    }

                    // Vẽ highlight theo ô
                    ClearHighlights();
                    var prefabToUse = selectedInfo.isWhite ? whiteHighlightPrefab : blackHighlightPrefab;
                    foreach (var cell in currentMoves)
                    {
                        var p = ChessBoardManager.Instance.BoardToWorld(cell.x, cell.y);
                        var obj = Instantiate(prefabToUse, p, Quaternion.identity);
                        activeSquares.Add(obj);
                    }

                    Debug.Log($"[Legal Moves] {selectedInfo.type} {(selectedInfo.isWhite ? "Trắng" : "Đen")} -> {currentMoves.Count} ô hợp lệ");
                }
            }
            else
            {
                ResetSelected();
                ClearHighlights();
            }
        }

        // ======================= HOVER =======================
        if (hitPiece)
        {
            GameObject pieceObj = hit.collider.gameObject;
            if (pieceObj != currentHover)
            {
                SoundManager.Instance?.PlayHover();
                ResetHover();

                hoverSkinController = pieceObj.GetComponent<ChessPieceSkinController>();
                if (hoverSkinController != null && hoverSkinController.CurrentState == SkinState.Normal)
                    hoverSkinController.SetSkinState(SkinState.Hover);
                else if (hoverSkinController == null)
                    ApplyHighlight(pieceObj, ref hoverOriginalMaterials);

                currentHover = pieceObj;
            }
        }
        else
        {
            ResetHover();
            currentHover = null;
        }

        // Log ô đang hover (plane-pick)
        if (ChessBoardManager.Instance != null && TryGetMouseCell(out var hoverCell, out var hoverWorld))
        {
            if (!_lastHoverCell.HasValue || _lastHoverCell.Value != hoverCell)
            {
                _lastHoverCell = hoverCell;
                if (Time.time - _lastLogTime > 0.03f)
                {
                    _lastLogTime = Time.time;
                    var snap = ChessBoardManager.Instance.BoardToWorld(hoverCell.x, hoverCell.y);
                    Debug.Log($"[HOVER] world={hoverWorld:F3} -> cell=({hoverCell.x},{hoverCell.y}) -> snap={snap:F3}");
                }
            }
        }
    }

    // ===================== Helpers =====================

    // Tự dò hàm trong moveGenerator (không phụ thuộc tên cụ thể)
    bool TryGetMovesFromGenerator(ChessPieceInfo info, out List<Vector2Int> outCells)
    {
        outCells = new List<Vector2Int>();
        if (moveGenerator == null) return false;

        var t = moveGenerator.GetType();
        // Bổ sung thêm tên nếu team bạn đặt khác
        string[] candidateNames = { "GetLegalMoves", "GetValidMoves", "GetMoves", "GenerateMoves", "GetPseudoLegalMoves" };

        foreach (var name in candidateNames)
        {
            var mi = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                 null, new[] { typeof(ChessPieceInfo) }, null);
            if (mi == null) continue;

            var result = mi.Invoke(moveGenerator, new object[] { info });
            if (result == null) continue;

            if (result is List<Vector2Int> cells)
            {
                outCells = cells;
                return true;
            }
            if (result is List<Vector3> worldList)
            {
                ConvertWorldListToCells(worldList, outCells);
                return true;
            }
        }
        return false;
    }

    // Chuyển danh sách world positions -> cells
    void ConvertWorldListToCells(List<Vector3> worldList, List<Vector2Int> outCells)
    {
        if (worldList == null) return;
        foreach (var w in worldList)
        {
            var c = ChessBoardManager.Instance.WorldToBoard(w);
            if (!outCells.Contains(c)) outCells.Add(c);
        }
    }

    // Plane-pick: tính ô từ tia chuột (hỗ trợ boardFrame nếu bạn dùng trong ChessBoardManager)
    bool TryGetMouseCell(out Vector2Int cell, out Vector3 hitPoint)
    {
        var cam = Camera.main;
        var ray = cam.ScreenPointToRay(Input.mousePosition);

        Plane plane;
        var mgr = ChessBoardManager.Instance;
        if (mgr.boardFrame != null)
            plane = new Plane(mgr.boardFrame.up, mgr.boardFrame.position);
        else
            plane = new Plane(Vector3.up, new Vector3(0f, mgr.boardOrigin.y, 0f));

        cell = default;
        hitPoint = default;
        if (!plane.Raycast(ray, out float enter)) return false;

        hitPoint = ray.GetPoint(enter);
        cell = mgr.WorldToBoard(hitPoint);
        return true;
    }

    void ClearHighlights()
    {
        foreach (var sq in activeSquares) if (sq) Destroy(sq);
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
        var renderers = piece.GetComponentsInChildren<SkinnedMeshRenderer>();
        originalMaterialsArray = new Material[renderers.Length][];

        var info = piece.GetComponent<ChessPieceInfo>();
        if (info == null) return;

        var mats = GetMaterialForPiece(info.isWhite, info.type);

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterialsArray[i] = renderers[i].sharedMaterials;
            var newMats = new Material[renderers[i].sharedMaterials.Length];
            for (int j = 0; j < newMats.Length; j++)
                newMats[j] = mats[Mathf.Min(j, mats.Length - 1)];
            renderers[i].sharedMaterials = newMats;
        }
    }

    void ResetHover()
    {
        if (currentHover == null) return;

        if (hoverSkinController != null)
        {
            hoverSkinController.SetSkinState(SkinState.Normal);
            hoverSkinController = null;
        }
        else
        {
            var renderers = currentHover.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterials = hoverOriginalMaterials[i];
        }

        currentHover = null;
    }

    private void OnPieceMoveCompleted(ChessPieceController pieceController)
    {
        pieceController.OnActionSequenceCompleted -= OnPieceMoveCompleted;
        isMoving = false;

        ResetSelected();
        ClearHighlights();

        Debug.Log($"Piece {pieceController.name} completed move sequence");
    }

    void ResetSelected()
    {
        if (currentSelected == null) return;

        if (selectedSkinController != null)
        {
            selectedSkinController.SetSkinState(SkinState.Normal);
            selectedSkinController = null;
        }
        else
        {
            var renderers = currentSelected.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterials = selectedOriginalMaterials[i];
        }

        currentSelected = null;
        selectedInfo = null;
    }
}
