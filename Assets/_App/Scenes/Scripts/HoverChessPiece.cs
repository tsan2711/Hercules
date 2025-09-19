using UnityEngine;

public class ChessRaycastDebug : MonoBehaviour
{
    private GameObject currentHover;
    private Material[][] originalMaterials;

    public enum ChessType
    {
        Pawn, Bishop, Rook, Horse, HorseRider, Queen, King
    }

    [System.Serializable]
    public class ChessMaterial
    {
        public bool isWhite;       // true = White, false = Black
        public ChessType type;     // loại quân
        public Material[] materials; // materials chuẩn của quân cờ này
    }

    public ChessMaterial[] chessMaterials;  // gán trong Inspector

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("ChessPiece")))
        {
            GameObject piece = hit.collider.gameObject;

            if (piece != currentHover)
            {
                ResetHighlight();

                // ✅ Lấy thông tin quân cờ
                ChessPieceInfo info = piece.GetComponent<ChessPieceInfo>();
                if (info != null)
                {
                    Material[] hoverMats = GetMaterialForPiece(info.isWhite, info.type);
                    if (hoverMats != null)
                    {
                        Debug.Log($"Hover vào: {piece.name} ({(info.isWhite ? "White" : "Black")} {info.type})");

                        // ✅ Highlight bằng materials tương ứng với loại quân
                        ApplyMaterialsToAllRenderers(piece, hoverMats);
                        currentHover = piece;
                    }
                    else
                    {
                        Debug.LogWarning($"Không tìm thấy materials cho {(info.isWhite ? "White" : "Black")} {info.type}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Object {piece.name} chưa có ChessPieceInfo!");
                }
            }
        }
        else if (currentHover != null)
        {
            ResetHighlight();
        }
    }

    Material[] GetMaterialForPiece(bool isWhite, ChessType type)
    {
        foreach (var cm in chessMaterials)
        {
            if (cm.isWhite == isWhite && cm.type == type)
                return cm.materials;
        }
        return null;
    }

    void ApplyMaterialsToAllRenderers(GameObject piece, Material[] newMats)
    {
        SkinnedMeshRenderer[] renderers = piece.GetComponentsInChildren<SkinnedMeshRenderer>();
        originalMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].materials; // lưu materials gốc
            Material[] mats = new Material[renderers[i].materials.Length];

            // gán newMats cho tất cả submesh
            for (int j = 0; j < mats.Length; j++)
                mats[j] = newMats[Mathf.Min(j, newMats.Length - 1)];

            renderers[i].materials = mats;
        }
    }

    void ResetHighlight()
    {
        if (currentHover == null || originalMaterials == null) return;

        SkinnedMeshRenderer[] renderers = currentHover.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].materials = originalMaterials[i];

        currentHover = null;
    }
}
