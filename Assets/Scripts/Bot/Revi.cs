using System;
using System.Collections.Generic;
using System.Diagnostics;

/* main weakness rn:
end game strength, it usally figures out what to do, but takes it sweet time, 
i thinks the cause is 1. the king from centre triggers 2 early, when 2 many pawns up or material advantage 2 small, secondly 
not enough encrougment to push pawns i think.

also just not very fast :(
*/


public static class Revi
{
    //search details

    public const int TranspositionChangeCap = 100;
    public const int SearchDepthIncreaseCap = 1000; //will rerun with increased search cap if not exceded

    public static int searchDepth = 3; //this number is one lower than the actual depth (4 is really searching 5 moves)
    static int searchDepthMaxExtend;

    static bool maximizingPlayer;
    static TranspositionTable transpositions;

    public static Dictionary<ulong, List<string>> openingBook = new Dictionary<ulong, List<string>>();

    //tracked stats (useful for debug, no effect on algorithm)

    static int moveSearchCount;
    static int branchesPrunned;
    static int potentialBranches;

    public static Move GetMove(Board board) //on ocasion the transpo table still makes errors but there now rare and small enough idrc
    {
        Stopwatch s = new Stopwatch();

        s.Start();

        if (openingBook.ContainsKey(board.zobristKey))
        {
            string openingBookMove = openingBook[board.zobristKey][UnityEngine.Random.Range(0, openingBook[board.zobristKey].Count)];
            UnityEngine.Debug.Log($"Move {openingBookMove} Selected Of {openingBook[board.zobristKey].Count} Book Moves, Time Taken {s.ElapsedMilliseconds}");
            return board.GetMove(openingBookMove);
        }

        moveSearchCount = 0;
        branchesPrunned = 0;
        potentialBranches = 0;
        transpositions = new TranspositionTable(searchDepth);

        maximizingPlayer = board.whiteTurn;
        //always end with the opponents move being considered, this stops the ai sacrificing a piece taking a knight or smthing without realising it can be captured back
        searchDepthMaxExtend = searchDepth % 2 == 0 ? -1 : 0;
        //EXPERMENTAL 

        (double eval, MoveNode move) move = AlphaBeta3(new Board(board), searchDepth, double.MinValue, double.MaxValue, board.whiteTurn);

        s.Stop();

        if (s.ElapsedMilliseconds < SearchDepthIncreaseCap)
        {
            searchDepth++;
            UnityEngine.Debug.Log($"Increasing Search Depth From {searchDepth - 1} To {searchDepth}\nTime Taken: {s.ElapsedMilliseconds}ms\nEval: {move.eval}, Index: {move.move.index}");
            Move m = GetMove(board);
            searchDepth--;
            return m;
        }

        UnityEngine.Debug.Log($"Moves Searched: {moveSearchCount}, Time Taken: {s.ElapsedMilliseconds}ms\nEval: {move.eval}, Index: {move.move.index}\nBranches Prunned: {branchesPrunned}, Potential Prunnes: {potentialBranches}");
        GUIHandler.UpdateBotUI(move.eval, moveSearchCount, s.Elapsed);

        return board.GetPlayerMoves()[move.move.index];
    }

    static (double eval, MoveNode index) AlphaBeta3(Board board, int depth, double alpha, double beta, bool whiteToPlay)
    {
        //reached end of depth or game final state been reached, so just evaluate current position (quite eval)
        if (board.gameProgress != 0 || (depth <= 0 && !board.majorCapture) || depth < searchDepthMaxExtend) return (Evaluation.Evaluate(board, maximizingPlayer), new MoveNode(-int.MaxValue, 0, null));

        if (depth > 0 && transpositions.Contains(board.zobristKey, depth)) //if pos prev found
        {
            potentialBranches++;
            (double eval, MoveNode index, double alphaBeta) t = transpositions.Get(board.zobristKey, depth); //get pos
            //beta or alpha has been expanded so much, old eval inaccurate and new eval required
            if (Math.Abs(t.alphaBeta - (whiteToPlay ? beta : alpha)) > TranspositionChangeCap)
            {
                transpositions.Remove(board.zobristKey, depth); //remove old eval
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

            foreach (Move move in board.GetPlayerMoves())
            {
                moveSearchCount++;

                board.MakeMove(move);

                (double value, MoveNode node) newValue = AlphaBeta3(board, depth - 1, alpha, beta, false);
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

            if (depth > 0) transpositions.Add(board.zobristKey, (value, chosenIndex, beta), depth);
            return (value, chosenIndex);
        }
        else //black
        {
            double value = double.MaxValue;

            foreach (Move move in board.GetPlayerMoves())
            {
                moveSearchCount++;

                board.MakeMove(move);

                (double value, MoveNode node) newValue = AlphaBeta3(board, depth - 1, alpha, beta, true);
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

            if (depth > 0) transpositions.Add(board.zobristKey, (value, chosenIndex, alpha), depth);
            return (value, chosenIndex);
        }
    }


}
