using System.Collections.Generic;

public interface IChessBoard
{
    string GetFen();
    List<string> GetMoveHistoryUci();
    void PlayMoveUci(string uci);
    bool IsAiTurn();
    bool IsGameOver();
}
