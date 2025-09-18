using UnityEngine;
using UnityEngine.UI;

public class ChessInput : MonoBehaviour
{
    public ChessController controller;
    public GridLayoutGroup grid;

    [Header("Board mapping")]
    public bool topLeftIsA8 = true; // Nếu bật: ô [0] = a8. Nếu tắt: ô [0] = a1.

    int? _fromIdx = null;

    void Start()
    {
        for (int i = 0; i < grid.transform.childCount; i++)
        {
            int idx = i;
            var cell = grid.transform.GetChild(i).gameObject;
            var btn = cell.GetComponent<Button>();
            if (!btn) btn = cell.AddComponent<Button>();
            btn.onClick.AddListener(() => OnCell(idx));
        }
    }

    async void OnCell(int idx)
    {
        string sq = IdxToUci(idx);
        Debug.Log((_fromIdx == null ? "Select from " : "Select to ") + $"idx={idx}, square={sq}");

        if (_fromIdx == null) { _fromIdx = idx; return; }

        int from = _fromIdx.Value;
        int to = idx;
        _fromIdx = null;

        string move = IdxToUci(from) + IdxToUci(to); // ví dụ e2e4
        Debug.Log("Try move: " + move);

        bool ok = await controller.TryPlayerMove(move);
        if (!ok) Debug.LogWarning("❌ Nước đi không hợp lệ hoặc controller chưa sẵn sàng");
    }

    // idx 0..63 -> ô cờ
    string IdxToUci(int idx)
    {
        int r = idx / 8; // 0..7
        int c = idx % 8; // 0..7
        char file = (char)('a' + c);
        char rank = topLeftIsA8 ? (char)('8' - r) : (char)('1' + r);
        return new string(new[] { file, rank });
    }
}
