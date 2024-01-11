using UnityEditor;

/// <summary> The info about a move on the board </summary>
public class Move
{
    public byte startPos; //pos move start
    public byte endPos; //pos move end

    public byte piece { get; private set; }
    public byte capturePiece { get; private set; }

    public byte type; //0 normal, 1 enpassanent, 2-5 promotion, 6-7 castle

    public BoardState state;

    /// <summary> Updates move with all data about itself, from current board position </summary>
    public void SetMoveInfo(Board board) //shouldnt really need calls outside of Board
    {
        piece = board.board[startPos];
        capturePiece = board.board[endPos];

        state = new BoardState(board.state);
    }

    public Move(Move move)
    {
        this.startPos = move.startPos;
        this.endPos = move.endPos;

        this.piece = move.piece;
        this.capturePiece = move.capturePiece;

        this.type = move.type;

        this.state = this.state == null ? new BoardState() : new BoardState(move.state);
    }

    public Move(byte startPos, byte endPos, byte type = 0)
    {
        this.startPos = startPos;
        this.endPos = endPos;
        this.type = type;
    }

    public static Move NullMove => new Move(255, 255, 255);
    public bool IsNullMove => type == 255;

    /// <summary> Hashcode for moves, unique for every move in a position (excluding promotion). </summary>
    public override int GetHashCode()
    {
        return startPos << 8 | endPos;
    }

    /// <summary> ToString, in format startpos : endpos : types. </summary>
    public override string ToString()
    {
        return $"{startPos} : {endPos} : {type}";
    }
}