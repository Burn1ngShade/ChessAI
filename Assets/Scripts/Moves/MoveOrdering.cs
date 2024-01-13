using System;
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
        (double white, double black, double total) remaingMaterial = Piece.RemaingMaterial(board); //material left on board (using rudmentray values)
        double interpFactor = Math.Clamp(remaingMaterial.total / Piece.MaxMaterial, 0, 1); //interpolate between midgame and endgame tables

        (Move move, int score)[] m = new (Move, int)[board.possibleMoves.Count];

        for (int i = 0; i < board.possibleMoves.Count; i++)
        {
            int score = 0;

            Move move = board.possibleMoves[i];
            byte type = board.board[move.startPos];
            byte captureType = board.board[move.endPos];
            bool isWhite = Piece.IsWhite(type);

            if (type <= 0 || type > 12)
            {
                UnityEngine.Debug.Log("THIS SHOULDNT BE HAPPENING!?!?!?!?");
                UnityEngine.Debug.Log(move);
            }

            bool recapturePossible = BinaryUtilities.BitboardContains(board.whiteTurn ? board.bPossbileAttackBitboard : board.wPossbileAttackBitboard, board.possibleMoves[i].endPos);

            if (board.board[move.endPos] != 0) //if its a capture
            {
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

            if (Piece.AbsoluteType(type) == 6)
            {
                if (move.type >= 2 && move.type <= 5) //promotion
                {
                    score += promotionBias;
                }
            }
            else if (Piece.AbsoluteType(type) == 1) { }
            else //not a capture
            {
                score += (int)Math.Floor((Piece.mgPieceTables[Piece.AbsoluteType(type) - 1][isWhite ? BinaryUtilities.FlipBitboardIndex(move.endPos) : move.endPos] * interpFactor +
                Piece.egPieceTables[Piece.AbsoluteType(type) - 1][isWhite ? BinaryUtilities.FlipBitboardIndex(move.endPos) : move.endPos]) * (1 - interpFactor));
                score -= (int)Math.Floor((Piece.mgPieceTables[Piece.AbsoluteType(type) - 1][isWhite ? BinaryUtilities.FlipBitboardIndex(move.startPos) : move.startPos] * interpFactor +
                Piece.egPieceTables[Piece.AbsoluteType(type) - 1][isWhite ? BinaryUtilities.FlipBitboardIndex(move.startPos) : move.startPos]) * (1 - interpFactor));

                //add something taking account of piece tables

                if (BinaryUtilities.BitboardContains(isWhite ? board.bPawnAttack : board.wPawnAttack, move.endPos))
                {
                    score -= 50;
                }
                else if (BinaryUtilities.BitboardContains(isWhite ? board.bAttackBitboard : board.wAttackBitboard, move.endPos)) //non can capture
                {
                    score -= 25;
                }
            }

            m[i] = (move, score);
        }

        m = m.OrderByDescending(x => x.score).ToArray();
        return m.Select(m => m.move).ToList();
    }
}
