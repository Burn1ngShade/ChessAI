using System;
using System.Collections.Generic;
using System.Diagnostics;

public static class Revi
{
    //search details

    public const int TranspositionChangeCap = 100;
    public const int SearchDepthIncreaseCap = 1000; //will rerun with increased search cap if not exceded

    public static int searchDepth = 4; //this number is one lower than the actual depth (4 is really searching 5 moves)
    static int searchDepthMaxExtend;

    static TranspositionTable transpositions;
    public static OpeningBook openingBook;

    //tracked stats (useful for debug, no effect on algorithm)

    static int moveSearchCount;
    static int branchesPrunned;
    static int potentialBranches;
    static Stopwatch s;

    public static Move GetMove(Board board, int searchDepth, bool increaseSearchDepth) //on ocasion the transpo table still makes errors but there now rare and small enough idrc
    {
        s = new Stopwatch();

        s.Start();

        if (openingBook.TryGetBookMove(board, out string moveString))
        {
            UnityEngine.Debug.Log($"Book Move Found: {moveString}");
            Move m = board.GetMove(moveString);
            GUIHandler.UpdateBotUI(m, 0, 0, 0, 0, 0, TimeSpan.Zero);
            return m;
        }

        moveSearchCount = 0;
        branchesPrunned = 0;
        potentialBranches = 0;
        transpositions = new TranspositionTable(searchDepth);

        //always end with the opponents move being considered, this stops the ai sacrificing a piece taking a knight or smthing without realising it can be captured back
        searchDepthMaxExtend = searchDepth % 2 == 0 ? -2 : -1;

        (double eval, MoveNode move) move = AlphaBeta4(new Board(board), searchDepth, double.MinValue, double.MaxValue, board.whiteTurn, new List<Move>());

        s.Stop();

        if (s.ElapsedMilliseconds < SearchDepthIncreaseCap && increaseSearchDepth)
        {
            UnityEngine.Debug.Log($"Increasing Search Depth From {searchDepth} To {searchDepth + 1}\nTime Taken: {s.ElapsedMilliseconds}ms\nEval: {move.eval}, Index: {move.move.index}");
            Move m = GetMove(board, searchDepth + 1, increaseSearchDepth);
            return m;
        }

        UnityEngine.Debug.Log($"Moves Searched: {moveSearchCount}, Time Taken: {s.ElapsedMilliseconds}ms\nEval: {move.eval}, Index: {move.move.index}, Depth: {move.move.depth}\nBranches Prunned: {branchesPrunned}, Potential Prunnes: {potentialBranches}");

        Move chosenMove = MoveOrdering.BasicOrderedMoves(board)[move.move.index];

        GUIHandler.UpdateBotUI(chosenMove, move.eval, moveSearchCount, searchDepth, potentialBranches, branchesPrunned, s.Elapsed);

        return chosenMove;
    }

    static (double eval, MoveNode index) AlphaBeta4(Board board, int depth, double alpha, double beta, bool whiteToPlay, List<Move> moves)
    {
        //reached end of depth or game final state been reached, so just evaluate current position (quite eval)
        if (board.state.gameState != 0 || (depth <= 0 && !board.majorEvent) || depth <= searchDepthMaxExtend)
        {
            return (Evaluation.Evaluate(board, moves), new MoveNode(-int.MaxValue, 0, null));
        }

        if (moves.Count != 0 && board.doublePreviousPositions.Contains(board.state.zobristKey))
        {
            return (0, new MoveNode(-int.MaxValue, 0, null)); //if position a repeat assumed to be a draw
        }

        if (depth > 0 && transpositions.Contains(board.state.zobristKey, depth)) //if pos prev found
        {
            potentialBranches++;
            (double eval, MoveNode index, double alphaBeta) t = transpositions.Get(board.state.zobristKey, depth); //get pos
            //beta or alpha has been expanded so much, old eval inaccurate and new eval required
            if (Math.Abs(t.alphaBeta - (whiteToPlay ? beta : alpha)) > TranspositionChangeCap)
            {
                transpositions.Remove(board.state.zobristKey, depth); //remove old eval
            }
            else
            {
                branchesPrunned++;
                return (t.eval, t.index);
            }
        }

        int index = 0;
        MoveNode chosenIndex = new MoveNode(-int.MaxValue, -int.MaxValue, null);

        if (whiteToPlay) //white player
        {
            double value = double.MinValue;

            foreach (Move move in MoveOrdering.BasicOrderedMoves(board))
            {
                moveSearchCount++;

                board.MakeMove(move);
                moves.Add(move);
                (double value, MoveNode node) newValue = AlphaBeta4(board, depth - 1, alpha, beta, false, moves);
                moves.RemoveAt(moves.Count - 1);
                if (value < newValue.value)
                {
                    chosenIndex = new MoveNode(index, newValue.node.depth, newValue.node);
                    value = newValue.value;
                }
                else if (value == newValue.value && chosenIndex.depth > newValue.node.depth) chosenIndex = new MoveNode(index, newValue.node.depth, newValue.node);
                index++;

                board.UndoMove();

                alpha = Math.Max(alpha, value);
                if (value > beta) break; //beta cutoff

            }

            chosenIndex.depth++;

            if (depth > 0) transpositions.Add(board.state.zobristKey, (value, chosenIndex, beta), depth);
            return (value, chosenIndex);
        }
        else //black
        {
            double value = double.MaxValue;

            foreach (Move move in MoveOrdering.BasicOrderedMoves(board))
            {
                moveSearchCount++;

                board.MakeMove(move);
                moves.Add(move);
                (double value, MoveNode node) newValue = AlphaBeta4(board, depth - 1, alpha, beta, true, moves);
                moves.RemoveAt(moves.Count - 1);
                if (value > newValue.value)
                {
                    chosenIndex = new MoveNode(index, newValue.node.depth, newValue.node);
                    value = newValue.value;
                }
                else if (value == newValue.value && chosenIndex.depth > newValue.node.depth) chosenIndex = new MoveNode(index, newValue.node.depth, newValue.node);
                index++;

                board.UndoMove();

                beta = Math.Min(beta, value);
                if (value < alpha) break; //alpha cutoff
            }

            chosenIndex.depth++;

            if (depth > 0) transpositions.Add(board.state.zobristKey, (value, chosenIndex, alpha), depth);
            return (value, chosenIndex);
        }
    }
}
