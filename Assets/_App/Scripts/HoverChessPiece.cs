using System.Collections.Generic;
using UnityEngine;

public class ChessRaycastDebug : MonoBehaviour
{
    private GameObject currentSelected;
    private Material[][] selectedOriginalMaterials;

    private GameObject currentHover;
    private Material[][] hoverOriginalMaterials;

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
                    pieceMover.MovePiece(selectedInfo, targetPos);
                    ResetSelected();
                    ClearHighlights();
                    return;
                }
            }

            if (hitPiece) // Click vào quân cờ
            {
                GameObject pieceObj = hit.collider.gameObject;
                ChessPieceInfo pieceInfo = pieceObj.GetComponent<ChessPieceInfo>();
                
                // Kiểm tra xem có phải lượt của người chơi này không
                if (pieceInfo != null && ChessBoardManager.Instance != null && !ChessBoardManager.Instance.CanPlayerMove(pieceInfo.isWhite))
                {
                    Debug.Log($"It's not {(pieceInfo.isWhite ? "White" : "Black")}'s turn!");
                    return;
                }
                
                if (pieceObj == currentSelected)
                {
                    ResetSelected();
                    ClearHighlights();
                }
                else
                {
                    ResetSelected();
                    ResetHover();

                    ApplyHighlight(pieceObj, ref selectedOriginalMaterials);
                    currentSelected = pieceObj;

                    selectedInfo = pieceObj.GetComponent<ChessPieceInfo>();
                    if (selectedInfo != null)
                    {
                        currentMoves = moveGenerator.GetMoves(selectedInfo);
                        ClearHighlights();
                        GameObject prefabToUse = selectedInfo.isWhite ? whiteHighlightPrefab : blackHighlightPrefab;

                        foreach (var pos in currentMoves)
                        {
                            GameObject obj = Instantiate(prefabToUse, pos, Quaternion.identity);
                            activeSquares.Add(obj);
                        }
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

            // Chỉ phát âm thanh khi hover vào 1 quân mới
            if (pieceObj != currentHover)
            {
                SoundManager.Instance.PlayHover(); // Phát 1 lần duy nhất khi đổi hover

                ResetHover();
                ApplyHighlight(pieceObj, ref hoverOriginalMaterials);
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
            originalMaterialsArray[i] = renderers[i].materials;
            Material[] newMats = new Material[renderers[i].materials.Length];
            for (int j = 0; j < newMats.Length; j++)
                newMats[j] = mats[Mathf.Min(j, mats.Length - 1)];
            renderers[i].materials = newMats;
        }
    }

    void ResetHover()
    {
        if (currentHover == null) return;
        SkinnedMeshRenderer[] renderers = currentHover.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].materials = hoverOriginalMaterials[i];
        currentHover = null;
    }

    void ResetSelected()
    {
        if (currentSelected == null) return;
        SkinnedMeshRenderer[] renderers = currentSelected.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].materials = selectedOriginalMaterials[i];
        currentSelected = null;
        selectedInfo = null;
    }
}
