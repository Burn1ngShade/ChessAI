using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Zobrist
{
    // Random numbers are generated for each aspect of the game state, and are used for calculating the hash:

    // piece type, colour, square index
    public static readonly ulong[,] piecesArray = new ulong[12, 64];
    // Each player has 4 possible castling right states: none, queenside, kingside, both.
    // So, taking both sides into account, there are 16 possible states.
    public static readonly ulong[] castlingRights = new ulong[16];
    // En passant file (0 = no ep).
    //  Rank does not need to be specified since side to move is included in key
    public static readonly ulong[] enPassantFile = new ulong[9];
    public static readonly ulong sideToMove;


    static Zobrist()
    {
        const int seed = 29426028;
        System.Random rng = new System.Random(seed);

        for (int squareIndex = 0; squareIndex < 64; squareIndex++)
        {
            for (int i = 0; i < 12; i++)
            {
                piecesArray[i, squareIndex] = RandomUnsigned64BitNumber(rng);
            }
        }

        for (int i = 0; i < castlingRights.Length; i++)
        {
            castlingRights[i] = RandomUnsigned64BitNumber(rng);
        }

        for (int i = 0; i < enPassantFile.Length; i++)
        {
            enPassantFile[i] = i == 0 ? 0 : RandomUnsigned64BitNumber(rng);
        }

        sideToMove = RandomUnsigned64BitNumber(rng);
    }

    // Calculate zobrist key from current board position.
    // NOTE: this function is slow and should only be used when the board is initially set up from fen.
    // During search, the key should be updated incrementally instead.
    public static ulong CalculateZobristKey(Board board)
    {
        ulong zobristKey = 0;

        for (int squareIndex = 0; squareIndex < 64; squareIndex++)
        {
            int piece = board.board[squareIndex];

            if (piece != 0)
            {
                zobristKey ^= piecesArray[piece - 1, squareIndex];
            }
        }

        zobristKey ^= enPassantFile[board.state.enPassantFile];

        if (!board.whiteTurn)
        {
            zobristKey ^= sideToMove;
        }

        zobristKey ^= castlingRights[board.state.enPassantFile];

        return zobristKey;
    }

    static ulong RandomUnsigned64BitNumber(System.Random rng)
    {
        byte[] buffer = new byte[8];
        rng.NextBytes(buffer);
        return System.BitConverter.ToUInt64(buffer, 0);
    }
}