using System;
using Unity.VisualScripting;
using UnityEngine;

public static class Evaluation
{
    //horizon effect is hitting

    public static double Evaluate(Board board, bool maximizingPlayer)
    {
        if (board.gameProgress != 0) //game finished
        {
            if (board.gameProgress == 1) return 99999;
            else if (board.gameProgress == 2) return -99999;
            else return 0;
        }

        (double white, double black, double total) remaingMaterial = Piece.RemaingMaterial(board); //material left on board (using rudmentray values)
        double interpFactor = Math.Clamp(remaingMaterial.total / Piece.MaxMaterial, 0, 1); //interpolate between midgame and endgame tables

        //piece and position values for all squares

        int mgEval = 0;
        int egEval = 0;

        for (int i = 0; i < 64; i++)
        {
            if ((board.pieceBitboard & (1UL << i)) == 0) continue; //no piece

            int isWhite = Piece.IsWhite(board.board[i]) ? 1 : -1;
            int absType = Piece.AbsoluteType(board.board[i]);

            mgEval += (Piece.mgPieceValues[absType - 1] + Piece.mgPieceTables[absType - 1][isWhite == 1 ? BinaryExtras.FlipBitboardIndex(i) : i]) * isWhite;
            egEval += (Piece.egPieceValues[absType - 1] + Piece.egPieceTables[absType - 1][isWhite == 1 ? BinaryExtras.FlipBitboardIndex(i) : i]) * isWhite;
        }

        double interpEval = (interpFactor * mgEval) + ((1 - interpFactor) * egEval);

        //mobility

        if (board.whiteTurn) interpEval += ((board.legalMoves - board.legalQueenMoves) - (board.oppLegalMoves - board.oppLegalQueenMoves)) * 9;
        else interpEval += ((board.oppLegalMoves - board.oppLegalQueenMoves) - (board.legalMoves - board.legalQueenMoves)) * 9;

        //endgame

        //problem is it dosnt prioritise moves that force king to centre earlier
        //if in endgame lets put king into the centre 

        if (1 - interpEval > 0.7) //if actually in an endgame
        {
            double eval = ForceKingFromCentre(board.whiteKingPos, board.blackKingPos, remaingMaterial.white, remaingMaterial.black) * (1 - interpFactor);
            if (board.wLastKingMove >= board.turn - Revi.searchDepth)
            {
                eval *= board.turn - board.wLastKingMove;
            }

            interpEval += eval;

            eval = ForceKingFromCentre(board.blackKingPos, board.whiteKingPos, remaingMaterial.black, remaingMaterial.white) * (1 - interpFactor);
            if (board.bLastKingMove >= board.turn - Revi.searchDepth)
            {
                eval *= board.turn - board.bLastKingMove;
            }

            interpEval -= eval;
        }

        return interpEval;
    }

    static double ForceKingFromCentre(int friendlyKingPos, int oppKingPos, double material, double oppositionMaterial)
    {
        if (material < oppositionMaterial) return 0;

        double eval = 0;

        int kingPosX = Piece.File(friendlyKingPos);
        int kingPosY = Piece.Rank(friendlyKingPos);

        int oppKingPosX = Piece.File(oppKingPos);
        int oppKingPosY = Piece.Rank(oppKingPos);

        //caculate distance of opponent king from centre, further is good!
        eval += (Math.Max(3 - oppKingPosX, oppKingPosX - 4) + Math.Max(3 - oppKingPosY, oppKingPosY - 4)) * 4;

        //caculate distance between 2 kings, closer is good!
        eval += (14 - (Math.Abs(kingPosX - oppKingPosX) + Math.Abs(kingPosY - oppKingPosY))) * 10;

        return eval;
    }
}
