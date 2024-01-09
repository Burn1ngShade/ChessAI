using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary> Class responsible for search algorithim move ordering. </summary>
public static class MoveOrdering
{
    const int winningCaptureBias = 800000;
    const int losingCaptureBias = 200000;
    const int promotionBias = 600000;

    //this tends to match speed or be slower over course of game?!?!?!?
    /// <summary> Advanced move ordering algorithim. </summary>
    public static List<Move> OrderedMoves(Board board)
    {
        (Move move, int score)[] m = new (Move, int)[board.possibleMoves.Count];

        for (int i = 0; i < board.possibleMoves.Count; i++)
        {
            int score = 0;

            Move move = board.possibleMoves[i];
            byte type = board.board[move.startPos];
            byte captureType = board.board[move.endPos];
            bool isWhite = Piece.IsWhite(type);

            bool recapturePossible = BinaryUtilities.BitboardContains(board.whiteTurn ? board.bPossbileAttackBitboard : board.wPossbileAttackBitboard, board.possibleMoves[i].endPos);

            if (board.board[move.endPos] != 0 && Piece.AbsoluteType(board.board[move.endPos]) != 6) //if its a capture
            {
                score += 10000;
                int captureMaterialDelta = Piece.SimplifiedMaterialValue(captureType) - Piece.SimplifiedMaterialValue(type);
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

                if (BinaryUtilities.BitboardContains(isWhite ? board.bAttackBitboard : board.wAttackBitboard, move.endPos)) //pawn can capture
                {
                    score -= 25;
                }
                else if (BinaryUtilities.BitboardContains(isWhite ? board.bPossbileAttackBitboard : board.wPossbileAttackBitboard, move.endPos)) //non pawn can capture
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

        m = m.OrderByDescending(x => x.score).ToArray();
        return m.Select(m => m.move).ToList();
    }

    /// <summary> Simplistic move ordering, similar performance in current bot but should fall behind with implementation of revi bot 2. </summary>
    public static List<Move> BasicOrderedMoves(Board board)
    {
        List<Move> m = new List<Move>();

        int maxValue = 1;

        int value;
        for (int i = 0; i < board.possibleMoves.Count; i++)
        {
            value = Piece.SimplifiedMaterialValue(board.board[board.possibleMoves[i].endPos]);

            if (maxValue <= value)
            {
                maxValue = value;
                m.Insert(0, board.possibleMoves[i]);
            }
            m.Add(board.possibleMoves[i]);
        }

        return m;
    }
}
