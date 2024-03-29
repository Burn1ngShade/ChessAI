using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

/// <summary> Class responsible for evaluating chess position. </summary>
public static class Evaluation
{
    //horizon effect is hitting

    /// <summary> Evaluates given chess position. </summary>
    public static double Evaluate(Board board, Move initalMove, int plyFromRoot = 0)
    {
        if (board.state.gameState != 0) //game finished
        {
            if (board.state.gameState == 1) return 99999 + (100 - plyFromRoot);
            else if (board.state.gameState == 2) return -99999 - (100 - plyFromRoot);
            else return 0;
        }

        (double white, double black, double total, double pawn) remaingMaterial = Piece.RemaingMaterial(board); //material left on board (using rudmentray values)
        double interpFactor = Math.Clamp(remaingMaterial.total / Piece.MaxMaterial, 0, 1); //interpolate between midgame and endgame tables

        //piece and position values for all squares

        (byte structure, byte pop)[] wPawns = new (byte, byte)[8]; //each array is equivalent to one rank, structure is pawns pop is the number
        (byte structure, byte pop)[] bPawns = new (byte, byte)[8];

        int mgEval = 0;
        int egEval = 0;

        for (int i = 0; i < 64; i++)
        {
            /* Checks for piece at given position, i 
            skipping to the next cycle of the loop if none found */
            if ((board.pieceBitboard & (1UL << i)) == 0) continue;

            int isWhite = Piece.IsWhite(board.board[i]) ? 1 : -1;
            int absType = Piece.AbsoluteType(board.board[i]);

            if (absType == 6)
            {
                /* If piece is white, white pawn structure of given file (x pos of piece) 
                is added to at given rank of index (y position of piece),
                before same is done to black.*/
                if (isWhite == 1) wPawns[Piece.File(i)].structure += (byte)(1 << Piece.Rank(i));
                else bPawns[Piece.File(i)].structure += (byte)(1 << Piece.Rank(i));
            }

            mgEval += (Piece.mgPieceValues[absType - 1] + Piece.mgPieceTables[absType - 1][isWhite == 1 ? BinaryUtilities.FlipBitboardIndex(i) : i]) * isWhite;
            egEval += (Piece.egPieceValues[absType - 1] + Piece.egPieceTables[absType - 1][isWhite == 1 ? BinaryUtilities.FlipBitboardIndex(i) : i]) * isWhite;
        }

        /* Midgame value is multiplied by interpFactor (midgame weight), and 
        added to the endgame value multiplied by 1 - interpFactor (endgame weight)
        resulting in a weighted average of the two values. */
        double eval = (interpFactor * mgEval) + ((1 - interpFactor) * egEval);

        //mobility

        if (!board.state.isCheck)
        {
            if (board.whiteTurn) eval += ((board.legalMoves.friendlyAll - board.legalMoves.friendlyQueen) - (board.legalMoves.oppAll - board.legalMoves.oppQueen)) * 9;
            else eval += ((board.legalMoves.oppAll - board.legalMoves.oppQueen) - (board.legalMoves.friendlyAll - board.legalMoves.friendlyQueen)) * 9;
        }

        //pawn structure

        for (int i = 0; i < 8; i++) //caculate count of pawns in each rank, saves recaculation multiple times later
        {
            wPawns[i].pop = BinaryUtilities.PopCount(wPawns[i].structure);
            bPawns[i].pop = BinaryUtilities.PopCount(bPawns[i].structure);
        }

        eval += EvaluatePawnStructure(wPawns, bPawns);
        eval -= EvaluatePawnStructure(bPawns, wPawns);

        //trade 
        if (remaingMaterial.white > remaingMaterial.black + 2) //ur really winning, so trading defo worth it
        {
            eval += ((Piece.MaxSideMaterial - remaingMaterial.black) / Piece.MaxSideMaterial) * 300;
        }
        else if (remaingMaterial.black > remaingMaterial.white + 2)
        {
            eval -= ((Piece.MaxSideMaterial - remaingMaterial.white) / Piece.MaxSideMaterial) * 300;
        }

        //king stuff

        //problem is it dosnt prioritise moves that force king to centre earlier
        //if in endgame lets put king into the centre 

        //you cant even being to comprehend how annoying this fu***** thing has been to get working, so the scuffed soloution must be allowed

        if (interpFactor < 0.4) //if actually in an endgame
        {
            double mopUpScore = ForceKingFromCentre(board.state.whiteKingPos, board.state.blackKingPos, remaingMaterial.white, remaingMaterial.black);
            if (!initalMove.IsNullMove && Piece.AbsoluteType(initalMove.piece) == 1 && remaingMaterial.black > 0)
            {
                mopUpScore *= remaingMaterial.white - remaingMaterial.black >= 5 && remaingMaterial.black == 1 ? 8 : 1.5;
            }

            eval += mopUpScore * (1 - interpFactor);

            mopUpScore = ForceKingFromCentre(board.state.blackKingPos, board.state.whiteKingPos, remaingMaterial.black, remaingMaterial.white);
            if (!initalMove.IsNullMove && Piece.AbsoluteType(initalMove.piece) == 1 && remaingMaterial.white > 0)
            {
                mopUpScore *= remaingMaterial.black - remaingMaterial.white >= 5 && remaingMaterial.white <= 1 ? 8 : 1.5;
            }

            eval -= mopUpScore * (1 - interpFactor);
        }
        if (interpFactor > 0.3)
        {
            eval += EvaluateKingSaftey(board.board, board.state.whiteKingPos, true) * interpFactor;
            eval -= EvaluateKingSaftey(board.board, board.state.blackKingPos, false) * interpFactor;

            //castling encouragement
            if (!initalMove.IsNullMove && (initalMove.type == 6 || initalMove.type == 7))
            {
                eval += 50 * ((board.turn - plyFromRoot) % 2 == 0 ? 1 : -1);
            }
        }

        return eval;
    }

    public static double RelativeEvaluate(Board board, Move initalMove, int plyFromRoot = 0)
    {
        return Evaluate(board, initalMove, plyFromRoot) * (board.whiteTurn ? 1 : -1);
    }

    /// <summary> Evaluates pawn structure (past pawns, doubled pawns ect) of current postion. </summary>
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
                eval -= (friendlyPawns[i].pop - 1) * 35; //punishment for pawn doubling
            }
            if (friendlyPawns[i].pop > 0 && (i == 0 ? 0 : friendlyPawns[i - 1].pop) + (i == 7 ? 0 : friendlyPawns[i + 1].pop) == 0)
            {
                eval -= 35; //punishment for isolated pawns
            }
            if (friendlyPawns[i].pop > 0 && oppPawns[i].pop + (i == 0 ? 0 : oppPawns[i - 1].pop) + (i == 7 ? 0 : oppPawns[i + 1].pop) == 0) //past pawn
            {
                eval += 60;
            }
        }

        return eval;
    }

    static (int pos, int value)[] pawnShieldData = new (int, int)[12]
    {
        (8, 4), (9, 7), (10, 4), //white left
        (13, 4), (14, 7), (15, 4), //white right
        (48, 4), (49, 7), (50, 4), //black left
        (53, 4), (54, 7), (55, 4) //black right
    };

    /// <summary> Evaluates king saftey (castled, pawn shield ect) of current postion. </summary>
    static double EvaluateKingSaftey(byte[] board, int kingPos, bool isWhite)
    {
        double eval = 0;

        int kingFile = Piece.File(kingPos);
        int kingRank = Piece.Rank(kingFile);

        if (kingFile >= 2 && kingFile <= 5) //king not castled hidden away
        {
            //smaller punishment for king side castle
            if (kingFile == 3) eval -= 15;
            else eval -= 70;
        }
        if ((isWhite && kingRank >= 2) || (!isWhite && kingRank <= 5)) //king in middle of board, tf you doing bro?
        {
            eval -= 90;
        }

        //pawn shield data offset
        if (kingFile >= 3 && kingFile <= 5) return eval;

        int shieldPenalty = 0;

        int psdOffset = (kingFile < 2 ? 0 : 3) + (isWhite ? 0 : 6);
        for (int i = psdOffset; i < psdOffset + 3; i++)
        {
            if (board[pawnShieldData[i].pos] != (isWhite ? 6 : 12))
            {
                shieldPenalty += pawnShieldData[i].value;
            }
        }

        shieldPenalty *= shieldPenalty; //square to heavly discourage pushing multiple pawns
        eval -= shieldPenalty;

        return eval;
    }

    /// <summary> Evaluates enemy kings position (distance from centre, distance from friendly king) of current postion. </summary>
    static double ForceKingFromCentre(int friendlyKingPos, int oppKingPos, double material, double oppositionMaterial)
    {
        if (material < oppositionMaterial + 2) return 0;

        double mopUpScore = 0;

        int kingPosX = Piece.File(friendlyKingPos);
        int kingPosY = Piece.Rank(friendlyKingPos);

        int oppKingPosX = Piece.File(oppKingPos);
        int oppKingPosY = Piece.Rank(oppKingPos);

        //caculate distance of opponent king from centre, further is good!
        mopUpScore += (Math.Max(3 - oppKingPosX, oppKingPosX - 4) + Math.Max(3 - oppKingPosY, oppKingPosY - 4)) * 4; //this value has to be higher to prevent king shuffling

        //caculate distance between 2 kings, closer is good!
        mopUpScore += (14 - (Math.Abs(kingPosX - oppKingPosX) + Math.Abs(kingPosY - oppKingPosY))) * 10;

        return mopUpScore;
    }
}
