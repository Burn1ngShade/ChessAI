/// <summary> Class for hashing position. </summary>
public static class Zobrist
{
    // piece type, colour, square index
    public static readonly ulong[,] piecesArray = new ulong[12, 64];
    public static readonly ulong[] castlingRights = new ulong[16];
    public static readonly ulong[] enPassantFile = new ulong[9];
    public static readonly ulong sideToMove;


    /// <summary> Initalise zobrist. </summary>
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

    /// <summary> Caculates zobrist key for given board (slow). </summary>
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

   /// <summary> Returns a pseudo-random ulong. </summary>
    static ulong RandomUnsigned64BitNumber(System.Random rng)
    {
        byte[] buffer = new byte[8];
        rng.NextBytes(buffer);
        return System.BitConverter.ToUInt64(buffer, 0);
    }
}