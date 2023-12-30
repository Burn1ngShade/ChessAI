using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Resources;
using UnityEngine;

public static class MoveGeneration
{
    static int checkState = 0;
    static int checker = 0;

    public static List<Move> GenerateMoves(Board b)
    {
        List<Move> newMoves = new List<Move>();

        ResetMoveGen(b);

        for (byte i = 0; i <= 63; i++)
        {
            if (b.board[i] != 0)
            {
                bool isWhite = Piece.IsWhite(b.board[i]);
                if (isWhite) b.wPieceBitboard += 1UL << i;
                else b.bPieceBitboard += 1UL << i;

                if (Piece.AbsoluteType(b.board[i]) == 1)
                {
                    if (isWhite) b.whiteKingPos = i;
                    else b.blackKingPos = i;
                }
            }

            switch (Piece.AbsoluteType(b.board[i]))
            {
                case 1: //king
                    GenerateKingMoves(b, i, ref newMoves);
                    break;
                case 2: //queen
                    GenerateLineMoves(b, i, 7, new int[] { -1, 8, 1, -8 }, ref newMoves);
                    GenerateLineMoves(b, i, 7, new int[] { -9, 9, 7, -7 }, ref newMoves);
                    break;
                case 3: //rook
                    GenerateLineMoves(b, i, 7, new int[] { -1, 8, 1, -8 }, ref newMoves);
                    break;
                case 4: //bishop
                    GenerateLineMoves(b, i, 7, new int[] { -9, 9, 7, -7 }, ref newMoves);
                    break;
                case 5: //knight
                    GenerateKnightMoves(b, i, ref newMoves);
                    break;
                case 6: //pawn
                    GeneratePawnMoves(b, i, ref newMoves);
                    break;
            }
        }

        b.pieceBitboard = b.wPieceBitboard | b.bPieceBitboard;

        GenerateAttackBitboards(b, newMoves);

        Stack<int> invalidMoves = new Stack<int>();

        ulong kingInvalidSquares = b.oppAttackBitboard | b.checkBitboard | b.kingBlockerBitboard;

        for (int i = 0; i < newMoves.Count; i++)
        {
            Move m = newMoves[i];

            int absType = Piece.AbsoluteType(b.board[m.startPos]);

            if (Piece.IsWhite(b.board[m.startPos]) != b.whiteTurn)
            {
                b.oppLegalMoves++;
                if (absType == 2) b.oppLegalQueenMoves++;
                continue;
            }

            if (absType == 1)
            {
                if ((kingInvalidSquares & (1UL << m.endPos)) != 0)
                {
                    invalidMoves.Push(i);
                    continue;
                }
            }
            else
            {
                if (checkState == 2)
                {
                    invalidMoves.Push(i); //cant block a double check STUPID.
                    continue;
                }

                if (checkState == 1)
                {
                    if ((b.checkBitboard & (1UL << m.endPos)) == 0 && m.endPos != checker) //not stoping the check STUPID
                    {
                        invalidMoves.Push(i);
                        continue;
                    }
                }

                if ((b.pinBitboard & (1UL << m.startPos)) != 0) //moving pinned piece
                {
                    if ((b.pinBitboard & (1UL << m.endPos)) == 0) //going to non pin spot
                    {
                        invalidMoves.Push(i);
                        continue;
                    }

                    (int x, int y) k = (b.friendlyKingPos % 8, b.friendlyKingPos / 8);
                    (int x, int y) s = (m.startPos % 8, m.startPos / 8);
                    (int x, int y) e = (m.endPos % 8, m.endPos / 8);

                    if (!((s.x > k.x ? 0 : s.x == k.x ? 1 : 2) == (e.x > k.x ? 0 : e.x == k.x ? 1 : 2) &&
                    (s.y > k.y ? 0 : s.y == k.y ? 1 : 2) == (e.y > k.y ? 0 : e.y == k.y ? 1 : 2)))
                    {
                        invalidMoves.Push(i);
                        continue;
                    }
                }
            }

            if (m.type == 6)
            {
                if (checkState != 0 || (b.oppAttackBitboard & (1UL << m.startPos - 1)) != 0)
                {
                    invalidMoves.Push(i);
                    continue;
                }
            }
            else if (m.type == 7)
            {
                if (checkState != 0 || (b.oppAttackBitboard & (1UL << m.startPos + 1)) != 0)
                {
                    invalidMoves.Push(i);
                    continue;
                }
            }

            if (Piece.EvalValue(b.board[m.endPos]) >= 3) b.majorCapture = true;

            b.legalMoves++;
            if (absType == 2) b.legalQueenMoves++;
        }

        while (invalidMoves.Count > 0)
        {
            newMoves.RemoveAt(invalidMoves.Pop());
        }

        //castle and checks must be edited

        GUIHandler.legalMoves = b.legalMoves;

        if (newMoves.Count < 5)
        {
            foreach (Move m in newMoves)
            {
                UnityEngine.Debug.Log(m.startPos + " : " + m.endPos);
            }
        }

        return newMoves;
    }

    static void ResetMoveGen(Board b)
    {
        b.wPieceBitboard = 0;
        b.bPieceBitboard = 0;
        b.pieceBitboard = 0;

        b.wAttackBitboard = 0;
        b.bAttackBitboard = 0;
        b.attackBitboard = 0;

        b.pinBitboard = 0;
        b.checkBitboard = 0;
        b.kingBlockerBitboard = 0;

        checkState = 0;
        checker = 0;

        b.legalMoves = 0;
        b.legalQueenMoves = 0;
        b.oppLegalMoves = 0;
        b.oppLegalQueenMoves = 0;

        b.majorCapture = false;
    }

    static void AddMove(Board b, ref List<Move> moves, Move move)
    {
        if (b.friendlyKingPos == move.endPos && move.type <= 2 && Piece.IsWhite(b.board[move.startPos]) != b.whiteTurn)
        {
            checker = move.startPos;
            checkState++;
        }
        moves.Add(move);
    }

    static void GenerateAttackBitboards(Board b, List<Move> moves)
    {
        foreach (Move move in moves)
        {
            int absType = Piece.AbsoluteType(b.board[move.startPos]);
            if (absType == 6 && move.startPos % 8 == move.endPos % 8) continue;

            bool isWhite = Piece.IsWhite(b.board[move.startPos]);

            if (isWhite && (b.wAttackBitboard & (1UL << move.endPos)) == 0) b.wAttackBitboard += 1UL << move.endPos;
            if (!isWhite && (b.bAttackBitboard & (1UL << move.endPos)) == 0) b.bAttackBitboard += 1UL << move.endPos;
        }

        b.attackBitboard = b.wAttackBitboard | b.bAttackBitboard;

        //pins
        byte kingPos = b.whiteTurn ? b.whiteKingPos : b.blackKingPos;
        (int x, int y) kp = (kingPos % 8, kingPos / 8);

        for (int k = 0; k < 64; k++)
        {
            if ((b.pieceBitboard & (1UL << k)) == 0) continue;

            (int x, int y) pp = (k % 8, k / 8);

            if (Piece.IsWhite(b.board[k]) == b.whiteTurn) continue;

            int absType = Piece.AbsoluteType(b.board[k]);

            if (absType == 2 || absType == 3)
            {
                if (kp.x == pp.x)
                {
                    int offset = pp.y - kp.y > 0 ? -8 : 8;
                    bool check = false;
                    int yDif = pp.y - kp.y;
                    ulong bitboard = 0;

                    int piecesFound = 0;

                    for (int i = 0; i <= Math.Abs(yDif); i++)
                    {
                        bitboard += 1UL << k + offset * i;

                        if (i == Math.Abs(yDif) && piecesFound == 0) check = true;
                        if (i != 0 && i != Math.Abs(yDif) && (b.pieceBitboard & (1UL << k + offset * i)) != 0) piecesFound++;
                    }

                    if (check)
                    {
                        bitboard &= ~(1UL << k);
                        bitboard &= ~(1UL << k + offset * Math.Abs(yDif));
                        int newIndex = k + offset * (Math.Abs(yDif) + 1);
                        if (newIndex >= 0 && newIndex < 64 && (b.kingBlockerBitboard & (1UL << newIndex)) == 0) b.kingBlockerBitboard += 1UL << k + offset * (Math.Abs(yDif) + 1);
                        b.checkBitboard |= bitboard;
                    }
                    else if (piecesFound == 1) b.pinBitboard |= bitboard;
                }
                else if (kp.y == pp.y)
                {
                    int offset = pp.x - kp.x > 0 ? -1 : 1;
                    bool check = false;
                    int xDif = pp.x - kp.x;
                    ulong bitboard = 0;

                    int piecesFound = 0;

                    for (int i = 0; i <= Math.Abs(pp.x - kp.x); i++)
                    {
                        bitboard += 1UL << k + offset * i;

                        if (i == Math.Abs(xDif) && piecesFound == 0) check = true;
                        if (i != 0 && i != Math.Abs(pp.x - kp.x) && (b.pieceBitboard & (1UL << k + offset * i)) != 0) piecesFound++;
                    }

                    if (check)
                    {
                        bitboard &= ~(1UL << k);
                        bitboard &= ~(1UL << k + offset * Math.Abs(xDif));
                        int newIndex = k + offset * (Math.Abs(xDif) + 1);
                        if (newIndex >= 0 && newIndex < 64 && (b.kingBlockerBitboard & (1UL << newIndex)) == 0) b.kingBlockerBitboard += 1UL << k + offset * (Math.Abs(xDif) + 1);
                        b.checkBitboard |= bitboard;
                    }
                    else if (piecesFound == 1) b.pinBitboard |= bitboard;
                }
            }
            if (absType == 2 || absType == 4)
            {
                if (Math.Abs(kp.x - pp.x) == Math.Abs(kp.y - pp.y))
                {
                    int offset;
                    if (kp.x > pp.x && kp.y > pp.y) offset = 9;
                    else if (kp.x > pp.x && kp.y < pp.y) offset = -7;
                    else if (kp.x < pp.x && kp.y > pp.y) offset = 7;
                    else offset = -9;

                    ulong bitboard = 0;
                    bool check = false;
                    int xDif = pp.x - kp.x;

                    int piecesFound = 0;

                    for (int i = 0; i <= Math.Abs(xDif); i++)
                    {
                        bitboard += 1UL << k + offset * i;

                        if (i == Math.Abs(xDif) && piecesFound == 0) check = true;

                        if (i != 0 && i != Math.Abs(xDif) && (b.pieceBitboard & (1UL << k + offset * i)) != 0) piecesFound++;
                    }

                    if (check)
                    {
                        bitboard &= ~(1UL << k);
                        bitboard &= ~(1UL << k + offset * Math.Abs(xDif));
                        int newIndex = k + offset * (Math.Abs(xDif) + 1);
                        if (newIndex >= 0 && newIndex < 64 && (b.kingBlockerBitboard & (1UL << newIndex)) == 0) b.kingBlockerBitboard += 1UL << k + offset * (Math.Abs(xDif) + 1);
                        b.checkBitboard |= bitboard;
                    }
                    else if (piecesFound == 1) b.pinBitboard |= bitboard;
                }

            }
        }
    }

    static void GeneratePawnMoves(Board b, byte index, ref List<Move> moves)
    {
        int isWhite = Piece.IsWhite(b.board[index]) ? 1 : -1;

        //move 

        int newIndex = index + 8 * isWhite;

        if (newIndex < 0 || newIndex > 63) return; //outside bounds of b
        if (b.board[newIndex] == 0)
        {
            if (newIndex / 8 == 0 || newIndex / 8 == 7)
            {
                for (int i = 2; i < 6; i++) AddMove(b, ref moves, new Move(index, (byte)newIndex, (byte)i));
            }
            else AddMove(b, ref moves, new Move(index, (byte)newIndex));
            if ((index / 8 == 1 && isWhite == 1) || (index / 8 == 6 && isWhite == -1))
            {
                newIndex += 8 * isWhite;
                if (b.board[newIndex] == 0) AddMove(b, ref moves, new Move(index, (byte)newIndex));
            }
        }

        //take
        newIndex = index + 5 * isWhite;
        for (int i = 0; i < 2; i++)
        {
            newIndex += 2 * isWhite;

            if (newIndex < 0 || newIndex > 63) continue; //outside bounds of b.b.board

            if (Math.Abs(newIndex % 8 - index % 8) == 7) continue;

            if (b.whiteTurn != (isWhite == 1) && ((b.kingBlockerBitboard & (1UL << newIndex)) == 0)) b.kingBlockerBitboard += 1UL << newIndex;

            if (b.board[newIndex] != 0 && Piece.IsWhite(b.board[newIndex]) != Piece.IsWhite(b.board[index]))
            {
                if (newIndex / 8 == 0 || newIndex / 8 == 7)
                {
                    for (int j = 2; j < 6; j++) AddMove(b, ref moves, new Move(index, (byte)newIndex, (byte)j));
                }
                else AddMove(b, ref moves, new Move(index, (byte)newIndex));
                continue;
            }

            if (b.previousMoves.Count > 0)
            {
                Move m = b.previousMoves.Peek();
                if (b.board[newIndex] == 0 && Piece.AbsoluteType(m.piece) == 6 && m.startPos == newIndex + (8 * isWhite) && m.endPos == newIndex - (8 * isWhite))
                {
                    AddMove(b, ref moves, new Move(index, (byte)newIndex, 1));
                }
            }
        }
    }

    static int[] knightOffsets = { -10, 6, 15, 17, 10, -6, -17, -15 };
    static int[] knightXOffsets = { -2, -2, -1, 1, 2, 2, -1, 1 };

    static void GenerateKnightMoves(Board b, byte index, ref List<Move> moves)
    {
        for (int i = 0; i < knightOffsets.Length; i++)
        {
            int newIndex = index + knightOffsets[i];
            if (newIndex < 0 || newIndex > 63 || index % 8 + knightXOffsets[i] > 7 || index % 8 + knightXOffsets[i] < 0) continue; //outside bounds of board or if overflow

            if (b.whiteTurn != Piece.IsWhite(b.board[index]) && ((b.kingBlockerBitboard & (1UL << newIndex)) == 0)) b.kingBlockerBitboard += 1UL << newIndex;
            if (b.board[newIndex] == 0 || (Piece.IsWhite(b.board[newIndex]) != Piece.IsWhite(b.board[index])))
            {
                AddMove(b, ref moves, new Move(index, (byte)newIndex));
            }
        }
    }

    static void GenerateLineMoves(Board b, byte index, byte maxLength, int[] indexShifts, ref List<Move> moves)
    {
        for (int j = 0; j < indexShifts.Length; j++) //each direction
        {
            for (int i = 1; i <= maxLength; i++)
            {
                int newIndex = index + (indexShifts[j] * i);

                if (newIndex % 8 == 0 && (index + (indexShifts[j] * (i - 1))) % 8 == 7) break;
                if (newIndex % 8 == 7 && (index + (indexShifts[j] * (i - 1))) % 8 == 0) break;

                if (newIndex < 0 || newIndex > 63) break; //outside bounds of board

                if (b.whiteTurn != Piece.IsWhite(b.board[index]) && ((b.kingBlockerBitboard & (1UL << newIndex)) == 0)) b.kingBlockerBitboard += 1UL << newIndex;

                if (b.board[newIndex] != 0)
                {
                    if (Piece.IsWhite(b.board[newIndex]) != Piece.IsWhite(b.board[index])) AddMove(b, ref moves, new Move(index, (byte)newIndex));
                    break;
                }

                AddMove(b, ref moves, new Move(index, (byte)newIndex));
            }
        }
    }

    static void GenerateKingMoves(Board b, byte index, ref List<Move> refMoves)
    {
        GenerateLineMoves(b, index, 1, new int[] { -1, 8, 1, -8 }, ref refMoves);
        GenerateLineMoves(b, index, 1, new int[] { -9, 9, 7, -7 }, ref refMoves);

        bool isWhite = Piece.IsWhite(b.board[index]);
        int shift = (isWhite ? 0 : 56);

        if ((b.castleRights & (1 << (isWhite ? 0 : 1))) == 0 && Piece.AbsoluteType(b.board[shift]) == 3 && b.board[shift + 1] + b.board[shift + 2] + b.board[shift + 3] == 0)
        {
            AddMove(b, ref refMoves, new Move(index, (byte)(index - 2), 6));
        }
        if ((b.castleRights & (1 << (isWhite ? 2 : 3))) == 0 && Piece.AbsoluteType(b.board[shift + 7]) == 3 && b.board[shift + 6] + b.board[shift + 5] == 0)
        {
            AddMove(b, ref refMoves, new Move(index, (byte)(index + 2), 7));
        }
    }
}
