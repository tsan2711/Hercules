using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ChessBoardAdapter : MonoBehaviour, IChessBoard
{
    [Header("Link t?i bàn c? g?c")]
    public ChessBoardManager board; // kéo component ChessBoardManager vào ?ây trong Inspector

    private readonly List<string> _moveHistoryUci = null;

    public string GetFen()
    {
        var sb = new StringBuilder();
        for (int r = 7; r >= 0; r--)
        {
            int empty = 0;
            for (int f = 0; f < 8; f++)
            {
                var info = board.board[f, r];
                if (info == null) { empty++; }
                else
                {
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    sb.Append(GetFenChar(info));
                }
            }
            if (empty > 0) sb.Append(empty);
            if (r != 0) sb.Append('/');
        }
        sb.Append(board.isWhiteTurn ? " w " : " b ");
        sb.Append("- - ");
        sb.Append("0 ");
        int fullmove = 1; sb.Append(fullmove);
        return sb.ToString();
    }

    public List<string> GetMoveHistoryUci() => _moveHistoryUci;

    public void PlayMoveUci(string uci)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) { Debug.LogWarning("Invalid UCI: " + uci); return; }

        int fx = uci[0] - 'a';
        int fy = uci[1] - '1';
        int tx = uci[2] - 'a';
        int ty = uci[3] - '1';
        char promo = uci.Length > 4 ? uci[4] : '\0';

        if (!InRange(fx, fy) || !InRange(tx, ty)) { Debug.LogWarning("UCI out of range: " + uci); return; }

        var piece = board.board[fx, fy];
        if (piece == null) { Debug.LogWarning($"No piece at {uci.Substring(0, 2)}"); return; }

        var target = board.board[tx, ty];
        if (target != null) { Destroy(target.gameObject); board.board[tx, ty] = null; }

        bool isPromotion = promo == 'q' || promo == 'r' || promo == 'b' || promo == 'n';
        if (isPromotion && IsPawn(piece))
        {
            Destroy(piece.gameObject);
            board.board[fx, fy] = null;

            bool isWhite = piece.isWhite;
            GameObject prefab = GetPromotionPrefab(isWhite, promo);
            var newObj = Instantiate(prefab, board.BoardToWorld(tx, ty), isWhite ? Quaternion.identity : Quaternion.Euler(0, 180, 0));
            var newInfo = newObj.GetComponent<ChessPieceInfo>();
            newInfo.isWhite = isWhite;
            newInfo.boardPosition = new Vector2Int(tx, ty);
            board.board[tx, ty] = newInfo;
        }
        else
        {
            board.UpdateBoardPosition(piece, new Vector2Int(tx, ty));
        }

        _moveHistoryUci?.Add(uci);
        board.EndTurn();
    }

    public bool IsAiTurn() => !board.isWhiteTurn;
    public bool IsGameOver() => false;

    // Helpers
    bool InRange(int x, int y) => x >= 0 && x < 8 && y >= 0 && y < 8;
    char GetFenChar(ChessPieceInfo info)
    {
        string n = info.gameObject.name.ToLowerInvariant();
        char c = n.Contains("rook") ? 'r' :
                 n.Contains("knight") ? 'n' :
                 n.Contains("bishop") ? 'b' :
                 n.Contains("queen") ? 'q' :
                 n.Contains("king") ? 'k' : 'p';
        return info.isWhite ? char.ToUpperInvariant(c) : c;
    }
    bool IsPawn(ChessPieceInfo info) => info.gameObject.name.ToLowerInvariant().Contains("pawn");
    GameObject GetPromotionPrefab(bool isWhite, char promo)
    {
        switch (char.ToLowerInvariant(promo))
        {
            case 'q': return isWhite ? board.whiteQueenPrefab : board.blackQueenPrefab;
            case 'r': return isWhite ? board.whiteRookPrefab : board.blackRookPrefab;
            case 'b': return isWhite ? board.whiteBishopPrefab : board.blackBishopPrefab;
            case 'n': return isWhite ? board.whiteKnightPrefab : board.blackKnightPrefab;
            default: return isWhite ? board.whiteQueenPrefab : board.blackQueenPrefab;
        }
    }
}
