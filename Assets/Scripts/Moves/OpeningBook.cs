using System;
using System.Collections.Generic;

/// <summary> Class responsible for holding opening book moves for chess bot. </summary>
public class OpeningBook
{
    readonly Dictionary<string, BookMove[]> movesByPosition;
    readonly Random rng;

    public OpeningBook(string file)
    {
        rng = new Random();
        Span<string> entries = file.Trim(new char[] { ' ', '\n' }).Split("pos").AsSpan(1);
        movesByPosition = new Dictionary<string, BookMove[]>(entries.Length);

        for (int i = 0; i < entries.Length; i++)
        {
            string[] entryData = entries[i].Trim('\n').Split('\n');
            string positionFen = entryData[0].Trim();
            Span<string> allMoveData = entryData.AsSpan(1);

            BookMove[] bookMoves = new BookMove[allMoveData.Length];

            for (int moveIndex = 0; moveIndex < bookMoves.Length; moveIndex++)
            {
                string[] moveData = allMoveData[moveIndex].Split(' ');
                bookMoves[moveIndex] = new BookMove(moveData[0], int.Parse(moveData[1]));
            }

            movesByPosition.Add(positionFen, bookMoves);
        }
    }

    /// <summary> Trys to select a move from current postion, picking randomly, according to number of times played and weight. </summary>
    public bool TryGetBookMoveWeighted(Board board, out string moveString, double weightPow = 0.5)
    {
        weightPow = Math.Clamp(weightPow, 0, 1);
        if (movesByPosition.TryGetValue(FormattingUtillites.SimplifiedFenString(FormattingUtillites.BoardToFenString(board)), out BookMove[] moves))
        {
            int totalPlayCount = 0;
            foreach (BookMove move in moves)
            {
                totalPlayCount += WeightedPlayCount(move.numTimesPlayed);
            }

            double[] weights = new double[moves.Length];
            double weightSum = 0;
            for (int i = 0; i < moves.Length; i++)
            {
                double weight = WeightedPlayCount(moves[i].numTimesPlayed) / (double)totalPlayCount;
                weightSum += weight;
                weights[i] = weight;
            }

            double[] probCumul = new double[moves.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                double prob = weights[i] / weightSum;
                probCumul[i] = probCumul[Math.Max(0, i - 1)] + prob;
            }


            double random = rng.NextDouble();
            for (int i = 0; i < moves.Length; i++)
            {
                if (random <= probCumul[i])
                {
                    moveString = moves[i].moveString;
                    return true;
                }
            }
        }

        moveString = "Null";
        return false;

        int WeightedPlayCount(int playCount) => (int)Math.Ceiling(Math.Pow(playCount, weightPow));
    }

    /// <summary> Trys to find book move in position, always play most played book move. </summary>
    public bool TryGetBookMove(Board board, out string moveString)
    {
        string positionFen = FormattingUtillites.SimplifiedFenString(FormattingUtillites.BoardToFenString(board));

        if (movesByPosition.TryGetValue(positionFen, out BookMove[] moves))
        {
            BookMove bestMove = new BookMove();
            int bestMoveCount = -int.MaxValue; //number of times played

            foreach (BookMove move in moves)
            {
                if (move.numTimesPlayed > bestMoveCount)
                {
                    bestMove = move;
                    bestMoveCount = move.numTimesPlayed;
                }
            }

            moveString = bestMove.moveString;
            return true;
        }

        moveString = "Null";
        return false;
    }

    /// <summary> Struct for a book move data. </summary>
    public readonly struct BookMove
    {
        public readonly string moveString;
        public readonly int numTimesPlayed;

        public BookMove(string moveString, int numTimesPlayed)
        {
            this.moveString = moveString;
            this.numTimesPlayed = numTimesPlayed;
        }
    }

}
