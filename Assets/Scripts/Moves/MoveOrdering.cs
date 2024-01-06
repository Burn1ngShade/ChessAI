using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class MoveOrdering
{
    const int winningCaptureBias = 800000;
    const int losingCaptureBias = 200000;
    const int promotionBias = 600000;

    //this does not work and is slower?!?!?!? lets gooooooo
    public static List<Move> OrderedMoves(Board board)
    {
        (Move move, int score)[] m = new (Move, int)[board.moves.Count];

        for (int i = 0; i < board.moves.Count; i++)
        {
            int score = 0;

            Move move = board.moves[i];
            byte type = board.board[move.startPos];
            byte captureType = board.board[move.endPos];
            bool isWhite = Piece.IsWhite(type);

            bool recapturePossible = BinaryExtras.BitboardContains(board.whiteTurn ? board.bPossbileAttackBitboard : board.wPossbileAttackBitboard, board.moves[i].endPos);

            if (board.board[move.endPos] != 0 && Piece.AbsoluteType(board.board[move.endPos]) != 6) //if its a capture
            {
                score += 10000;
                int captureMaterialDelta = Piece.MaterialValue(captureType) - Piece.MaterialValue(type); 
                if (recapturePossible)
                {
                    //8 if capture is postive for us, else 2 if negative
                    score += (captureMaterialDelta >= 0 ? winningCaptureBias : losingCaptureBias) + captureMaterialDelta; 
                }
                else
                {
                    score += winningCaptureBias + captureMaterialDelta;
                }
            }
            else //not a capture
            {
                //add something taking account of piece tables

                if (BinaryExtras.BitboardContains(isWhite ? board.bAttackBitboard : board.wAttackBitboard, move.endPos)) //pawn can capture
                {
                    score -= 25;
                }
                else if (BinaryExtras.BitboardContains(isWhite ? board.bPossbileAttackBitboard : board.wPossbileAttackBitboard, move.endPos)) //non pawn can capture
                {
                    score -= 50;
                }
            }

            if (move.type >= 2 && move.type <= 5) //promotion
            {
                score += promotionBias;
            }

            m[i] = (move, score);
        }

        m = m.OrderBy(x => x.score).ToArray();
        return m.Select(m => m.move).ToList();
    }

    public static List<Move> BasicOrderedMoves(Board board)
    {
        List<Move> m = new List<Move>();

        int maxValue = 1;

        int value;
        for (int i = 0; i < board.moves.Count; i++)
        {
            value = Piece.MaterialValue(board.board[board.moves[i].endPos]);

            if (maxValue <= value)
            {
                maxValue = value;
                m.Insert(0, board.moves[i]);
            }
            m.Add(board.moves[i]);
        }

        return m;
    }
}
