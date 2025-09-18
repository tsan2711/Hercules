using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChessView : MonoBehaviour
{
    [Header("References")]
    public GridLayoutGroup grid;            // Kéo Board (có GridLayoutGroup) vào ?ây

    [Header("Style")]
    public TMP_FontAsset pieceFont;
    public Color lightColor = new Color(0.90f, 0.90f, 0.90f); // ô sáng
    public Color darkColor = new Color(0.45f, 0.65f, 0.85f); // ô t?i
    public int fontSize = 48;

    private TextMeshProUGUI[] _labels;      // 64 label hi?n th? quân
    private Image[] _cells;                  // 64 n?n ô (Image)

    void Awake()
    {
        if (!grid) grid = GetComponent<GridLayoutGroup>();
        BuildBoardIfNeeded();
    }

    /// <summary>T?o 64 ô n?u ch?a có: m?i ô là 1 GameObject (Image) và 1 child TextMeshProUGUI.</summary>
    void BuildBoardIfNeeded()
    {
        var parent = grid.transform;

        // Xóa c? n?u s? ô khác 64
        if (parent.childCount != 64)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);

            for (int i = 0; i < 64; i++)
            {
                var cellGO = new GameObject($"Cell_{i}", typeof(RectTransform), typeof(Image));
                cellGO.transform.SetParent(parent, false);
                var cellImage = cellGO.GetComponent<Image>();
                cellImage.raycastTarget = true;

                var labelGO = new GameObject("Label", typeof(RectTransform));
                labelGO.transform.SetParent(cellGO.transform, false);
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = fontSize;
                tmp.enableWordWrapping = false;
                tmp.raycastTarget = false;

                if (pieceFont != null) tmp.font = pieceFont;   // <-- GÁN FONT ? ?ÂY

                var rt = (RectTransform)labelGO.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }

        }

        // Cache m?ng
        _cells = new Image[64];
        _labels = new TextMeshProUGUI[64];
        for (int i = 0; i < 64; i++)
        {
            var cell = parent.GetChild(i);
            _cells[i] = cell.GetComponent<Image>();
            _labels[i] = cell.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // Tô caro ban ??u
        RefreshCheckerColors();
    }

    /// <summary>Tô màu caro cho 64 ô (idx 0 là a8, 63 là h1).</summary>
    void RefreshCheckerColors()
    {
        for (int i = 0; i < 64; i++)
        {
            int r = i / 8; // 0..7 (trên?d??i)
            int c = i % 8; // 0..7 (trái?ph?i)
            _cells[i].color = ((r + c) % 2 == 0) ? lightColor : darkColor;
        }
    }

    /// <summary>Render theo FEN: "rnbqkbnr/pppppppp/8/.../ w KQkq - 0 1"</summary>
    public void RenderFen(string fen)
    {
        if (_labels == null || _labels.Length != 64) BuildBoardIfNeeded();

        var board = FenToArray(fen);
        for (int i = 0; i < 64; i++)
            _labels[i].text = PieceToGlyph(board[i]);
    }

    /// <summary>Chuy?n ph?n bàn c? trong FEN (tr??c d?u cách) thành m?ng 64 ký t?.</summary>
    char[] FenToArray(string fen)
    {
        var field = fen.Split(' ')[0];
        var rows = field.Split('/');
        var list = new List<char>(64);
        foreach (var row in rows)
        {
            foreach (var ch in row)
            {
                if (char.IsDigit(ch))
                {
                    int n = ch - '0';
                    for (int i = 0; i < n; i++) list.Add(' ');
                }
                else list.Add(ch);
            }
        }
        return list.ToArray();
    }

    /// <summary>Map ký t? FEN ? ký t? Unicode quân c?.</summary>
    string PieceToGlyph(char p)
    {
        switch (p)
        {
            case 'K': return "♔";
            case 'Q': return "♕";
            case 'R': return "♖";
            case 'B': return "♗";
            case 'N': return "♘";
            case 'P': return "♙";
            case 'k': return "♚";
            case 'q': return "♛";
            case 'r': return "♜";
            case 'b': return "♝";
            case 'n': return "♞";
            case 'p': return "♟";
            default: return "";
        }
    }
}
