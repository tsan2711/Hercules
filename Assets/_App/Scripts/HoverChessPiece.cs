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
    public float tileSize = 2f; // khoảng cách giữa các ô

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool hitPiece = Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("ChessPiece"));

        // Click logic
        if (Input.GetMouseButtonDown(0))
        {
            if (hitPiece)
            {
                GameObject pieceObj = hit.collider.gameObject;

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

                    ChessPieceInfo info = pieceObj.GetComponent<ChessPieceInfo>();
                    if (info != null)
                        Highlight2Ahead(info);
                }
            }
            else
            {
                ResetSelected();
                ClearHighlights();
            }
        }

        // Hover logic (chỉ khi chưa chọn)
        if (hitPiece)
        {
            GameObject pieceObj = hit.collider.gameObject;
            if (pieceObj != currentSelected && pieceObj != currentHover)
            {
                ResetHover();
                ApplyHighlight(pieceObj, ref hoverOriginalMaterials);
                currentHover = pieceObj;
            }
        }
        else
        {
            ResetHover();
        }
    }

    // Spawn highlight 2 ô trước dựa trên vị trí Transform, prefab tùy màu quân
    void Highlight2Ahead(ChessPieceInfo piece)
    {
        ClearHighlights();

        Vector3 piecePos = piece.transform.position;
        int direction = piece.isWhite ? 1 : -1; // hướng di chuyển
        Vector3 highlightPos = piecePos + new Vector3(0, 0.1f, 2 * tileSize * direction);

        GameObject prefabToUse = piece.isWhite ? whiteHighlightPrefab : blackHighlightPrefab;

        GameObject obj = Instantiate(prefabToUse, highlightPos, Quaternion.identity);
        activeSquares.Add(obj);
    }

    // Xóa toàn bộ highlight cũ
    void ClearHighlights()
    {
        foreach (var sq in activeSquares)
            Destroy(sq);
        activeSquares.Clear();
    }

    public enum ChessType
    {
        Pawn, Bishop, Rook, Knight, Queen, King
    }

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
    }
}
