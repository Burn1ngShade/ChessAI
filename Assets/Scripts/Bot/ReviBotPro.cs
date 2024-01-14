using System;
using System.Collections.Generic;
using System.Diagnostics;

// --- TO IMPLEMENT ---
//generate capture only boards
//smarter iterative deepening
//pgn load support

/// <summary> Chess engines primary chess bot, est rating 1600 - 1800 assuming use of smart preset (tested up to 1500, easily beating opp). </summary>
public static class ReviBotPro
{
    //iterative deeping
    public static readonly int[] iterativeThresholds = new int[3] {250, 1000, 2500};

    //settings
    public const int MaxExtensionCount = 16; //max number of times a search will be deepened to look at something

    //search stuff
    public static OpeningBook openingBook;
    static SearchDiagnostics diagnostics;
    static TranspositionTable transpositionTable;

    //references
    static Board board;

    static Move searchInitalMove;
    static Move bestMove;


    public static void Init()
    {
        openingBook = new OpeningBook(System.IO.File.ReadAllText("Assets/Book.txt"));
        transpositionTable = new TranspositionTable(64); //64 mb
        GUIHandler.ResetBotUI();
    }

    /// <summary> Starts a bot search with given param, returns best move found during search. </summary>
    public static Move StartSearch(Board searchBoard, GameHandler.IterativeDeepeningMode iterativeDeepening, int searchDepth, int openBookMode, int rootSearch = 0) //on ocasion the transpo table still makes errors but there now rare and small enough idrc
    {
        //apply param
        board = new Board(searchBoard);

        if (board.turn == 0 || diagnostics == null)
        {
            diagnostics = new SearchDiagnostics(); //reset debug if new game
            transpositionTable.Clear();
        }

        //book moves
        Move bookMove = TryGetBookMove(openBookMode);
        if (!bookMove.IsNullMove)
        {
            bestMove = bookMove;
            diagnostics.currentMove = new SearchDiagnostics.MoveDiagnostics(0, 0, 1, 0);
            diagnostics.moveDiagnostics.Add(diagnostics.currentMove);
            diagnostics.OutputMoveDiagnostics();
            return bookMove;
        }

        //non book move

        diagnostics.StartMoveDiagnostics();

        (double eval, int finalDepth) searchResults = IterativeDeepening(searchDepth, iterativeDeepening);

        diagnostics.currentMove.eval = searchResults.eval;
        diagnostics.currentMove.depth = searchResults.finalDepth;
        diagnostics.StopMoveDiagnostics();

        return bestMove;
    }

    static (double evaluation, int finalDepth) IterativeDeepening(int intialSearchDepth, GameHandler.IterativeDeepeningMode iterativeDeepening)
    {
        int currentSearchDepth = intialSearchDepth;
        double eval = Search(currentSearchDepth, 0, double.MinValue, double.MaxValue, 0);

        if (iterativeDeepening == GameHandler.IterativeDeepeningMode.Off) return (eval, intialSearchDepth);

        while (true)
        {
            if (diagnostics.searchElapsedTime < iterativeThresholds[(int)iterativeDeepening - 1])
            {
                currentSearchDepth++;
                UnityEngine.Debug.Log($"[ReviBotPro], Total Search Elapsed Time: {diagnostics.searchElapsedTime}ms, Increasing Search Depth From {currentSearchDepth - 1} To {currentSearchDepth}");
                Search(currentSearchDepth, 0, double.MinValue, double.MaxValue, 0);
            }
            else
            {
                break;
            }
        }

        return (eval, currentSearchDepth);
    }

    /// <summary> Main search function for bot. </summary>
    static double Search(int plyRemaining, int plyFroomRoot, double alpha, double beta, int extensions)
    {
        //game is not still being played (won/drawn), so can get value of gamestate from evaluation
        if (board.state.gameState > 0)
        {
            double gameEndEval = Evaluation.RelativeEvaluate(board, searchInitalMove, plyFroomRoot);
            return gameEndEval;
        }

        if (plyFroomRoot > 0) //if not first move
        {
            if (board.doublePreviousPositions.Contains(board.state.zobristKey))
            {
                return 0; //if position a repeat assumed to be a draw
            }
        }

        double ttEval = transpositionTable.LookupEvaluation(board, plyRemaining, plyFroomRoot, alpha, beta);
        //transpos table has value
        if (ttEval != -1)
        {
            if (plyFroomRoot == 0)
            {
                bestMove = transpositionTable.GetMove(board);
            }
            diagnostics.currentMove.transpositions++;
            return ttEval;
        }

        if (plyRemaining == 0) //search over, so switch to QuiesceneSearch
        {
            return QuiescenceSearch(alpha, beta, plyFroomRoot);
        }

        List<Move> orderedMoves = MoveOrdering.OrderedMoves(board); //order moves so most promising moves come first
        byte evaluationBounds = TranspositionTable.UpperBound;
        Move bestMoveInPosition = Move.NullMove;

        for (int i = 0; i < orderedMoves.Count; i++)
        {
            Move move = orderedMoves[i];
            bool isCapture = board.board[move.endPos] != 0;
            int absType = Piece.AbsoluteType(board.board[move.startPos]);

            if (plyFroomRoot == 0) searchInitalMove = move;
            diagnostics.currentMove.movesSearched++;

            board.MakeMove(move);

            //extend the search depth of promising moves

            int extend = 0;
            if (extensions < MaxExtensionCount)
            {
                if (board.state.isCheck) //check so checkmate could be close
                {
                    extend = 1;
                }
                else if (absType == Piece.PawnPiece && (Piece.Rank(move.endPos) == 6 || Piece.Rank(move.endPos) == 1)) //one off from promotion, want to see if promo possible
                {
                    extend = 1;
                }
            }

            double eval = 0;
            bool needsFullSearch = true;
            if (extend == 0 && plyRemaining >= 3 && i >= 3 && !isCapture) //reduce search if move is unlikey to be any good
            {
                eval = -Search(plyRemaining - 2, plyFroomRoot + 1, -alpha - 1, -alpha, extensions);
                needsFullSearch = eval > alpha; //if eval is better than alpha then worth checking
            }
            if (needsFullSearch) eval = -Search(plyRemaining - 1 + extend, plyFroomRoot + 1, -beta, -alpha, extensions + extend);

            board.UndoMove();

            if (eval >= beta)
            {
                transpositionTable.StoreEvaluation(board, (byte)plyRemaining, plyFroomRoot, beta, TranspositionTable.LowerBound, orderedMoves[i]);
                return beta;
            }

            if (eval > alpha)
            {
                evaluationBounds = TranspositionTable.Exact;
                bestMoveInPosition = orderedMoves[i];

                alpha = eval;

                if (plyFroomRoot == 0)
                {
                    bestMove = move;
                }
            }
        }

        transpositionTable.StoreEvaluation(board, (byte)plyRemaining, plyFroomRoot, alpha, evaluationBounds, bestMoveInPosition);
        return alpha;
    }

    /// <summary> Extention of standard search, searches until postion is quite (no captures). </summary>
    static double QuiescenceSearch(double alpha, double beta, int plyFromRoot)
    {
        double eval = Evaluation.RelativeEvaluate(board, searchInitalMove, plyFromRoot); //get base eval
        if (eval >= beta)
        {
            return beta;
        }
        if (eval > alpha)
        {
            alpha = eval;
        }

        List<Move> moves = MoveOrdering.OrderedMoves(board);
        for (int i = 0; i < moves.Count; i++)
        {
            if (board.board[moves[i].endPos] == 0) continue; //we are only checking captures

            diagnostics.currentMove.movesSearched++;

            board.MakeMove(moves[i]);
            eval = -QuiescenceSearch(-beta, -alpha, plyFromRoot + 1);
            board.UndoMove();

            if (eval >= beta)
            {
                return beta;
            }
            if (eval > alpha)
            {
                alpha = eval;
            }
        }

        return alpha;
    }

    /// <summary> Attempt to find current position in opening book, and returns associated position. </summary>
    public static Move TryGetBookMove(int openBookMode)
    {
        if (openBookMode == -1 && openingBook.TryGetBookMove(board, out string moveString))
        {
            UnityEngine.Debug.Log($"Book Move Found: {moveString}");
            Move m = board.GetMove(moveString);
            return m;
        }
        else if (openBookMode >= 0 && openingBook.TryGetBookMoveWeighted(board, out string weightMoveString, (double)openBookMode / 4))
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
        public double searchElapsedTime => searchWatch.ElapsedMilliseconds;

        public void StartMoveDiagnostics()
        {
            if (searchWatch.IsRunning) return;

            currentMove = new MoveDiagnostics();

            searchWatch.Start();
        }

        public void StopMoveDiagnostics()
        {
            currentMove.timeTaken = searchWatch.Elapsed.TotalSeconds;
            searchWatch.Reset();

            if (currentMove.movesSearched == 0) currentMove.moveType = 2;
            totalMovesSearched += currentMove.movesSearched;
            totalTimeTaken += currentMove.timeTaken;
            moveDiagnostics.Add(currentMove);


            OutputMoveDiagnostics();
        }

        public void OutputMoveDiagnostics()
        {
            GUIHandler.UpdateBotUI(bestMove, currentMove);

            if (currentMove.moveType == 1)
            {
                UnityEngine.Debug.Log("[ReviBotPro] Book Move");
                return;
            }

            string output = $"[ReviBotPro] Total Moves Searched: {totalMovesSearched}, Total Time Taken: {Math.Round(totalTimeTaken, 2)}s\n";
            output += $"Move Eval: {currentMove.eval}, Move Depth: {currentMove.depth}, Moves Searched {currentMove.movesSearched}, Time Taken: {Math.Round(currentMove.timeTaken, 2)}s\n";
            output += $"Transpositions: {currentMove.transpositions}";
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
            public int transpositions = 0;

            public double eval;
            public int depth;

            public int moveType = 0;
            public double timeTaken = 0;

            public MoveDiagnostics() : this(0, 0, 0, 0) { }

            public MoveDiagnostics(int movesSearched, int transpositions, int moveType, double timeTaken)
            {
                this.movesSearched = movesSearched;
                this.transpositions = transpositions;
                this.moveType = moveType;
                this.timeTaken = timeTaken;
            }
        }
    }
}
