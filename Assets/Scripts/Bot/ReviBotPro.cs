using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using Unity.VisualScripting;

//to implement
//get alpha beta pruning actually workign!!!!
//better extend algorithim
// quiescience search
// reduce depth for irrelevant moves

public static class ReviBotPro
{
    //settings
    public static bool useDynamicDepth = true;
    public static int searchDepth = 4; //this number is one lower than the actual depth (4 is really searching 5 moves)
    public static int openingBookMode = -1; //-2 off -1 on 0 1 2 3 4 are rng

    static int searchDepthMaxExtend;

    //search details

    public const int TranspositionChangeCap = 100;
    public const int SearchDepthIncreaseCap = 1000; //will rerun with increased search cap if not exceded

    static TranspositionTablePro transpositions;

    //opening books stuff
    public static OpeningBook openingBook => ReviBot.openingBook;
    static SearchDiagnostics diagnostics;

    //references
    static Board board;

    static Move searchInitalMove;
    static Move bestMove;


    /// <summary> Get move with given settings, according to bots search algorithim. </summary>
    public static Move StartSearch(Board searchBoard, int searchDepth, bool dynamicSearchDepth, int openingBookMode) //on ocasion the transpo table still makes errors but there now rare and small enough idrc
    {
        board = new Board(searchBoard);

        if (board.turn == 0 || diagnostics == null) diagnostics = new SearchDiagnostics();

        Move bookMove = TryGetBookMove();
        if (!bookMove.IsNullMove)
        {
            diagnostics.currentMove = new SearchDiagnostics.MoveDiagnostics(0, 0, 0, true, 0);
            diagnostics.moveDiagnostics.Add(diagnostics.currentMove);
            diagnostics.OutputMoveDiagnostics();
            return bookMove;
        }

        diagnostics.StartMoveDiagnostics();

        //always end with the opponents move being considered, this stops the ai sacrificing a piece taking a knight or smthing without realising it can be captured back
        searchDepthMaxExtend = searchDepth % 2 == 0 ? -2 : -1;
        transpositions = new TranspositionTablePro(searchDepth + (searchDepth % 2 == 0 ? 3 : 2));

        double eval = Search(searchDepth, 0, double.MinValue, double.MaxValue);

        // if (s.ElapsedMilliseconds < SearchDepthIncreaseCap && dynamicSearchDepth)
        // {
        //     UnityEngine.Debug.Log($"[PRO] Increasing Search Depth From {searchDepth} To {searchDepth + 1}\nTime Taken: {s.ElapsedMilliseconds}ms\nEval: {move.eval}, Index: {move.move.index}");
        //     Move m = StartSearch(board, searchDepth + 1, dynamicSearchDepth, openingBookMode);
        //     return m;
        // }

        //converts to side to move relative 
        diagnostics.currentMove.eval = eval;
        diagnostics.currentMove.depth = searchDepth;
        diagnostics.StopMoveDiagnostics();

        GUIHandler.UpdateBotUI(bestMove, eval * (searchBoard.whiteTurn ? 1 : -1), diagnostics.currentMove.movesSearched, searchDepth, diagnostics.currentMove.potentialBranches, diagnostics.currentMove.branchesPrunned, TimeSpan.FromSeconds(diagnostics.currentMove.timeTaken));

        return bestMove;
    }

    /// <summary> 4th iteration of alpha beta search algorithim for the bot (non side to move relative). </summary>
    static double Search(int plyRemaining, int plyFroomRoot, double alpha, double beta)
    {
        //reached end of depth or game final state been reached, so just evaluate current position (quite eval)
        if (board.state.gameState > 0 || (plyRemaining <= 0 && !board.state.isCaptureOrPromotion && !board.state.isCheck) || plyRemaining <= searchDepthMaxExtend)
        {
            return Evaluation.RelativeEvaluate(board, searchInitalMove, plyFroomRoot);
        }

        if (plyFroomRoot > 0)
        {
            if (board.doublePreviousPositions.Contains(board.state.zobristKey))
            {
                return 0; //if position a repeat assumed to be a draw
            }

            if (transpositions.Contains(board.state.zobristKey, plyFroomRoot))
            {
                diagnostics.currentMove.potentialBranches++;
                double eval = transpositions.Get(board.state.zobristKey, plyFroomRoot); //get pos

                diagnostics.currentMove.branchesPrunned++;
                return eval;
            }
        }

        List<Move> orderedMoves = MoveOrdering.OrderedMoves(board);

        for (int i = 0; i < orderedMoves.Count; i++)
        {
            Move move = orderedMoves[i];
            bool isCapture = board.board[move.endPos] != 0;

            if (plyFroomRoot == 0) searchInitalMove = move;
            diagnostics.currentMove.movesSearched++;

            board.MakeMove(move);

            double eval = 0;
            bool needsFullSearch = true;
            if (plyRemaining >= 3 && i >= 3 && !isCapture)
            {
                eval = -Search(plyRemaining - 2, plyFroomRoot + 1, -beta, -alpha);
                needsFullSearch = eval > alpha;
            }
            if (needsFullSearch) eval = -Search(plyRemaining - 1, plyFroomRoot + 1, -beta, -alpha);

            board.UndoMove();

            if (eval >= beta)
            {
                return beta;
            }

            if (eval > alpha)
            {
                alpha = eval;

                if (plyFroomRoot == 0)
                {
                    bestMove = move;
                }
            }
        }

        if (plyFroomRoot > 0) transpositions.Add(board.state.zobristKey, alpha, plyFroomRoot);
        return alpha;
    }

    static Move TryGetBookMove()
    {
        if (openingBookMode == -1 && openingBook.TryGetBookMove(board, out string moveString))
        {
            UnityEngine.Debug.Log($"Book Move Found: {moveString}");
            Move m = board.GetMove(moveString);
            return m;
        }
        else if (openingBookMode >= 0 && openingBook.TryGetBookMoveWeighted(board, out string weightMoveString, (double)openingBookMode / 4))
        {
            UnityEngine.Debug.Log($"Book Move Found: {weightMoveString}");
            Move m = board.GetMove(weightMoveString);
            return m;
        }
        return Move.NullMove;
    }

    /// <summary> Class responsible for tracking data about searching</summary>
    public class SearchDiagnostics
    {
        public int totalMovesSearched = 0;
        public double totalTimeTaken = 0;

        public List<MoveDiagnostics> moveDiagnostics;
        public MoveDiagnostics currentMove;

        Stopwatch searchWatch = new Stopwatch();

        public void StartMoveDiagnostics()
        {
            currentMove = new MoveDiagnostics();

            searchWatch.Start();
        }

        public void StopMoveDiagnostics()
        {
            currentMove.timeTaken = searchWatch.Elapsed.TotalSeconds;
            searchWatch.Reset();

            totalMovesSearched += currentMove.movesSearched;
            totalTimeTaken += currentMove.timeTaken;
            moveDiagnostics.Add(currentMove);

            OutputMoveDiagnostics();
        }

        public void OutputMoveDiagnostics()
        {
            if (currentMove.bookMove)
            {
                UnityEngine.Debug.Log("[ReviBotPro] Book Move");
                return;
            }

            string output = $"[ReviBotPro] Total Moves Searched: {totalMovesSearched}, Total Time Taken: {totalTimeTaken}\n";
            output += $"Move Eval: {currentMove.eval}, Move Depth: {currentMove.depth}, Moves Searched {currentMove.movesSearched}, Time Taken: {currentMove.timeTaken}\n";
            output += $"Potential Transpositions: {currentMove.potentialBranches}, Transpositions Prunned: {currentMove.branchesPrunned}";
            UnityEngine.Debug.Log(output);
        }

        public SearchDiagnostics()
        {
            totalMovesSearched = 0;
            totalTimeTaken = 0;
            moveDiagnostics = new List<MoveDiagnostics>();
        }

        public class MoveDiagnostics
        {
            public int movesSearched = 0;
            public int branchesPrunned = 0;
            public int potentialBranches = 0;

            public double eval;
            public int depth;

            public bool bookMove = false;
            public double timeTaken = 0;

            public MoveDiagnostics() : this(0, 0, 0, false, 0) { }

            public MoveDiagnostics(int movesSearched, int branchesPrunned, int potentialBranches, bool bookMove, double timeTaken)
            {
                this.movesSearched = movesSearched;
                this.branchesPrunned = branchesPrunned;
                this.potentialBranches = potentialBranches;
                this.bookMove = bookMove;
                this.timeTaken = timeTaken;
            }
        }
    }
}
