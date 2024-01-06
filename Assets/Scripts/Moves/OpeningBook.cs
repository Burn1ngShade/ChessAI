using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class OpeningBook
{
    readonly Dictionary<string, BookMove[]> movesByPosition;
    readonly System.Random rng;

    public OpeningBook(string file)
    {
        rng = new System.Random();
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

    // WeightPow is a value between 0 and 1.
    // 0 means all moves are picked with equal probablity, 1 means moves are weighted by num times played.
    public bool TryGetBookMoveWeighted(Board board, out string moveString, double weightPow = 0.5)
    {
        string positionFen = ChessExtras.BoardToFenString(board);
        weightPow = Math.Clamp(weightPow, 0, 1);
        if (movesByPosition.TryGetValue(ChessExtras.SimplifiedFenString(positionFen), out BookMove[] moves))
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

    public bool TryGetBookMove(Board board, out string moveString)
    {
        string positionFen = ChessExtras.SimplifiedFenString(ChessExtras.BoardToFenString(board));

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
