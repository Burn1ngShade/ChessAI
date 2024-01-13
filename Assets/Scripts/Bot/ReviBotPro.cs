using System;
using System.Collections.Generic;
using System.Diagnostics;

//to implement
//generate capture only boards
//check mates on  chheky eval ygm cuh

/// <summary> Chess engines primary chess bot, est rating 1500 - 1700 (tested up to 1400, easily beating opp). </summary>
public static class ReviBotPro
{
    //settings
    public static bool useDynamicDepth = true;
    public static int searchDepth = 4; //this number is one lower than the actual depth (4 is really searching 5 moves)
    public static int openingBookMode = -1; //-2 off -1 on 0 1 2 3 4 are rng
    //search details

    public const int TranspositionChangeCap = 100;
    public const int SearchDepthIncreaseCap = 1200; //will rerun with increased search cap if not exceded
    public const int MaxExtensionCount = 16; //max number of times a search will be deepened to look at something

    static TranspositionTable transpositionTable;

    //opening books stuff
    public static OpeningBook openingBook;
    static SearchDiagnostics diagnostics;

    //references
    static Board board;

    static Move searchInitalMove;
    static Move bestMove;


    public static void Init()
    {
        openingBook = new OpeningBook(System.IO.File.ReadAllText("Assets/Book.txt"));
        transpositionTable = new TranspositionTable(64); //64 mb
    }

    /// <summary> Starts a bot search with given param, returns best move found during search. </summary>
    public static Move StartSearch(Board searchBoard, int searchDepth, bool dynamicDepth, int openBookMode) //on ocasion the transpo table still makes errors but there now rare and small enough idrc
    {
        //apply param
        openingBookMode = openBookMode;
        useDynamicDepth = dynamicDepth;
        board = new Board(searchBoard);

        if (board.turn == 0 || diagnostics == null)
        {
            diagnostics = new SearchDiagnostics(); //reset debug if new game
            transpositionTable.Clear();
        }

        //book moves
        Move bookMove = TryGetBookMove();
        if (!bookMove.IsNullMove)
        {
            diagnostics.currentMove = new SearchDiagnostics.MoveDiagnostics(0, 0, 0, true, 0);
            diagnostics.moveDiagnostics.Add(diagnostics.currentMove);
            diagnostics.OutputMoveDiagnostics();
            return bookMove;
        }

        //non book move

        diagnostics.StartMoveDiagnostics();
        //transpositions = new TranspositionTablePro(searchDepth + (searchDepth % 2 == 0 ? 3 : 2) + MaxExtensionCount);

        double eval = Search(searchDepth, 0, double.MinValue, double.MaxValue, 0);

        //if the time taken to generate a move is deemed short enough, depth is increased as it shouldnt take to long (iterative deepening)
        //also checks that enabled, and that it's the previous check was not a mate, cause if it's a mate no point checking deeper
        if (diagnostics.searchElapsedTime < SearchDepthIncreaseCap && useDynamicDepth && Math.Abs(eval) < 99999) 
        {
            UnityEngine.Debug.Log($"[ReviBotPro], Total Search Elapsed Time: {diagnostics.searchElapsedTime}ms, Increasing Search Depth From {searchDepth} To {searchDepth + 1}");
            Move m = StartSearch(board, searchDepth + 1, useDynamicDepth, openingBookMode);
            return m;
        }

        // update debug
        diagnostics.currentMove.eval = eval;
        diagnostics.currentMove.depth = searchDepth;
        diagnostics.StopMoveDiagnostics();

        // update ui
        GUIHandler.UpdateBotUI(bestMove, eval * (searchBoard.whiteTurn ? 1 : -1), diagnostics.currentMove.movesSearched, searchDepth, diagnostics.currentMove.potentialBranches, diagnostics.currentMove.branchesPrunned, TimeSpan.FromSeconds(diagnostics.currentMove.timeTaken));

        return bestMove;
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
                UnityEngine.Debug.Log(transpositionTable.positions[board.state.zobristKey % transpositionTable.positionCount].eval);
                bestMove = transpositionTable.GetMove(board);
            }
            diagnostics.currentMove.branchesPrunned++;
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

            string output = $"[ReviBotPro] Total Moves Searched: {totalMovesSearched}, Total Time Taken: {Math.Round(totalTimeTaken, 2)}s\n";
            output += $"Move Eval: {currentMove.eval}, Move Depth: {currentMove.depth}, Moves Searched {currentMove.movesSearched}, Time Taken: {Math.Round(currentMove.timeTaken, 2)}s\n";
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
