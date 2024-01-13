using System;
using Unity.Mathematics;

public class TranspositionTable
{
    public const int Exact = 0;
    public const int LowerBound = 1;
    public const int UpperBound = 2;

    //number of entrys the transposition table can hold
    public readonly ulong positionCount;
    public Position[] positions;

    public TranspositionTable(int size) //size in megabyte
    {
        int tableEntrySize = System.Runtime.InteropServices.Marshal.SizeOf<Position>();
        int tableByteSize = size * 1024 * 1024; //converting from mb to kb to b

        positionCount = (ulong)(tableByteSize / tableEntrySize);
        positions = new Position[positionCount];
    }

    public void Clear()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = new Position();
        }
    }

    public double LookupEvaluation(Board board, int depth, int plyFromRoot, double alpha, double beta)
    {
        Position position = positions[board.state.zobristKey % positionCount]; //i do not know why there is a modulas here icl, using implementation inspired by sebastion lague

        if (position.zobristKey == board.state.zobristKey)
        {
            double eval = CorrectRetrievedMateScore(position.eval, plyFromRoot);
            //only use if more or as good as depth being as current search
            //or it's a mate, cause searching deeper is guaranteeded for the same result
            if (position.depth >= depth || Math.Abs(eval) > 99999) 
            {
                if (position.evalType == Exact) return eval;

                if (position.evalType == UpperBound && eval <= alpha) //worse than best move we found, and cause its upper bound, it cant be any better!
                {
                    return eval;
                }

                if (position.evalType == LowerBound && eval >= beta) //we have worse possible value, and best move for opponent is lower, so we dont care!
                {
                    return eval;
                }
            }
        }

        return -1; //lookup fail
    }

    public void StoreEvaluation(Board board, byte depth, int plyFromRoot, double eval, byte evalType, Move move)
    {
        Position position = new Position(board.state.zobristKey, move, CorrectMateScoreForStorage(eval, plyFromRoot), evalType, depth);
        positions[board.state.zobristKey % positionCount] = position;
    }

    public Move GetMove(Board board)
    {
        return positions[board.state.zobristKey % positionCount].move;
    }

    double CorrectMateScoreForStorage(double eval, int numPlySearched)
    {
        if (Math.Abs(eval) >= 99999)
        {
            int sign = System.Math.Sign(eval);
            return (eval * sign + numPlySearched) * sign;
        }
        return eval;
    }

    double CorrectRetrievedMateScore(double eval, int numPlySearched)
    {
        if (Math.Abs(eval) >= 99999)
        {
            int sign = System.Math.Sign(eval);
            return (eval * sign - numPlySearched) * sign;
        }
        return eval;
    }

    public struct Position
    {
        public readonly ulong zobristKey;
        public readonly Move move;

        public readonly byte depth; //how deep the search went from this point

        public readonly double eval;
        public readonly byte evalType; //type of eval reached

        public Position(ulong zobristKey, Move move, double eval, byte evalType, byte depth)
        {
            this.zobristKey = zobristKey;
            this.move = move;

            this.eval = eval;
            this.evalType = evalType;

            this.depth = depth;
        }
    }
}
