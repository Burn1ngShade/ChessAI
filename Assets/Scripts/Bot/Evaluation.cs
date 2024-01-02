using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class Evaluation
{
    //horizon effect is hitting

    public static double Evaluate(Board board)
    {
        if (board.state.gameState != 0) //game finished
        {
            if (board.state.gameState == 1) return 99999;
            else if (board.state.gameState == 2) return -99999;
            else return 0;
        }

        (double white, double black, double total) remaingMaterial = Piece.RemaingMaterial(board); //material left on board (using rudmentray values)
        double interpFactor = Math.Clamp(remaingMaterial.total / Piece.MaxMaterial, 0, 1); //interpolate between midgame and endgame tables

        //piece and position values for all squares

        (byte structure, byte pop)[] wPawns = new (byte, byte)[8]; //each array is equivalent to one rank, structure is pawns pop is the number
        (byte structure, byte pop)[] bPawns = new (byte, byte)[8];

        int mgEval = 0;
        int egEval = 0;

        for (int i = 0; i < 64; i++)
        {
            if ((board.pieceBitboard & (1UL << i)) == 0) continue; //no piece

            int isWhite = Piece.IsWhite(board.board[i]) ? 1 : -1;
            int absType = Piece.AbsoluteType(board.board[i]);

            if (absType == 6)
            {
                if (isWhite == 1) wPawns[i % 8].structure += (byte)(1 << i / 8);
                else bPawns[i % 8].structure += (byte)(1 << i / 8);
            }

            mgEval += (Piece.mgPieceValues[absType - 1] + Piece.mgPieceTables[absType - 1][isWhite == 1 ? BinaryExtras.FlipBitboardIndex(i) : i]) * isWhite;
            egEval += (Piece.egPieceValues[absType - 1] + Piece.egPieceTables[absType - 1][isWhite == 1 ? BinaryExtras.FlipBitboardIndex(i) : i]) * isWhite;
        }

        double interpEval = (interpFactor * mgEval) + ((1 - interpFactor) * egEval);

        //mobility

        if (!board.isCheck)
        {
            if (board.whiteTurn) interpEval += ((board.legalMoves.friendlyAll - board.legalMoves.friendlyQueen) - (board.legalMoves.oppAll - board.legalMoves.oppQueen)) * 9;
            else interpEval += ((board.legalMoves.oppAll - board.legalMoves.oppQueen) - (board.legalMoves.friendlyAll - board.legalMoves.friendlyQueen)) * 9;
        }

        //pawn structure

        for (int i = 0; i < 8; i++) //caculate count of pawns in each rank, saves recaculation multiple times later
        {
            wPawns[i].pop = BinaryExtras.PopCount(wPawns[i].structure);
            bPawns[i].pop = BinaryExtras.PopCount(bPawns[i].structure);
        }

        interpEval += EvaluatePawnStructure(wPawns, bPawns);
        interpEval -= EvaluatePawnStructure(bPawns, wPawns);

        //trade 
        if (remaingMaterial.white > remaingMaterial.black + 2) //ur really winning, so trading defo worth it
        {
            interpEval += ((Piece.MaxSideMaterial - remaingMaterial.white) / Piece.MaxSideMaterial) * 300; 
        }
        else if (remaingMaterial.black > remaingMaterial.white + 2)
        {
            interpEval += ((Piece.MaxSideMaterial - remaingMaterial.black) / Piece.MaxSideMaterial) * 300;
        }

        //king stuff

        //problem is it dosnt prioritise moves that force king to centre earlier
        //if in endgame lets put king into the centre 

        if (1 - interpEval > 0.7) //if actually in an endgame
        {
            double eval = ForceKingFromCentre(board.whiteKingPos, board.blackKingPos, remaingMaterial.white, remaingMaterial.black);
            if (board.state.wLastKingMove == board.turn - (Revi.searchDepth + 2) && remaingMaterial.black > 0)
            {
                eval *= 5;
            }

            interpEval += eval;

            eval = ForceKingFromCentre(board.blackKingPos, board.whiteKingPos, remaingMaterial.black, remaingMaterial.white);
            if (board.state.bLastKingMove == board.turn - (Revi.searchDepth + 2) && remaingMaterial.white > 0)
            {
                eval *= 5;
            }

            interpEval -= eval;
        }
        else
        {
            int kingFile = Piece.File(board.whiteKingPos);

            if (kingFile >= 2 && kingFile <= 5) 
            {
                interpEval -= 100;
            }

            kingFile = Piece.File(board.blackKingPos);
            if (kingFile >= 2 && kingFile <= 5)
            {
                interpEval += 100;
            }
        }

        return interpEval;
    }

    static double EvaluatePawnStructure((byte structure, byte pop)[] friendlyPawns, (byte structure, byte pop)[] oppPawns)
    {
        //isolated pawns -> pawn without neighbours on opposing ranks
        //passed pawns -> pawn with no enemy pawns on in front of it, in 3 near colloums
        //doubled/trippled pawns -> bad

        int eval = 0;

        for (int i = 0; i < 8; i++)
        {
            if (friendlyPawns[i].pop > 1)
            {
                eval -= (friendlyPawns[i].pop - 1) * 40; //punishment for pawn doubling
            }
            if (friendlyPawns[i].pop > 0 && (i == 0 ? 0 : friendlyPawns[i - 1].pop) + (i == 7 ? 0 : friendlyPawns[i + 1].pop) == 0)
            {
                eval -= 40; //punishment for isolated pawns
            }
            if (friendlyPawns[i].pop > 0 && (i == 0 ? 0 : oppPawns[i - 1].pop) + (i == 7 ? 0 : oppPawns[i + 1].pop) == 0) //past pawn
            {
                eval += 70;
            }
        }

        return eval;
    }

    static double ForceKingFromCentre(int friendlyKingPos, int oppKingPos, double material, double oppositionMaterial)
    {
        if (material < oppositionMaterial + 2) return 0;

        double mopUpScore = 0;

        int kingPosX = Piece.File(friendlyKingPos);
        int kingPosY = Piece.Rank(friendlyKingPos);

        int oppKingPosX = Piece.File(oppKingPos);
        int oppKingPosY = Piece.Rank(oppKingPos);

        //caculate distance of opponent king from centre, further is good!
        mopUpScore += (Math.Max(3 - oppKingPosX, oppKingPosX - 4) + Math.Max(3 - oppKingPosY, oppKingPosY - 4)) * 8; //this value has to be higher to prevent king shuffling

        //caculate distance between 2 kings, closer is good!
        mopUpScore += (14 - (Math.Abs(kingPosX - oppKingPosX) + Math.Abs(kingPosY - oppKingPosY))) * 20;

        return mopUpScore;
    }
}
