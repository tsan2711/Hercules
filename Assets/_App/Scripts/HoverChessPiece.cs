using UnityEngine;

public class ChessRaycastDebug : MonoBehaviour
{
    private GameObject currentSelected;
    private Material[][] selectedOriginalMaterials;

    private GameObject currentHover;
    private Material[][] hoverOriginalMaterials;

    public enum ChessType
    {
        Pawn, Bishop, Rook, Horse, HorseRider, Queen, King
    }

    [System.Serializable]
    public class ChessMaterial
    {
        public bool isWhite;
        public ChessType type;
        public Material[] materials;
    }

    public ChessMaterial[] chessMaterials;

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool hitPiece = Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("ChessPiece"));

        // Click logic
        if (Input.GetMouseButtonDown(0))
        {
            if (hitPiece)
            {
                GameObject piece = hit.collider.gameObject;

                if (piece == currentSelected)
                {
                    // Click lại quân đã chọn → bỏ highlight
                    ResetSelected();
                }
                else
                {
                    ResetSelected();
                    ResetHover();
                    ApplyHighlight(piece, ref selectedOriginalMaterials);
                    currentSelected = piece;
                }
            }
            else
            {
                // Click ngoài → bỏ highlight
                ResetSelected();
            }
        }

        // Hover logic (chỉ khi chưa chọn)
        if (hitPiece)
        {
            GameObject piece = hit.collider.gameObject;
            if (piece != currentSelected && piece != currentHover)
            {
                ResetHover();
                ApplyHighlight(piece, ref hoverOriginalMaterials);
                currentHover = piece;
            }
        }
        else
        {
            ResetHover();
        }
    }

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
