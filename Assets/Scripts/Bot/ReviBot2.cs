using System;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary> 2nd Iteration chess bot of chess engine, contains search engine. [WIP] </summary>
public static class ReviBot2
{
    const int MaxExtensions = 3;

    const int forcedMateScore = 100000; //mate in the position has been forced

    //references 
    // static Board board;
    static BotSearchData searchData;
    static BotSearchDiagnostics searchDiagnostics;

    public static Board board;

    public static Move InitalizeSearch(Board searchBoard, BotSearchSettings searchSettings)
    {
        Stopwatch s = new Stopwatch();
        s.Start();

        board = new Board(searchBoard);
        searchData = new BotSearchData(searchSettings);
        searchDiagnostics = new BotSearchDiagnostics();

        if (searchSettings.openBookMode == -1 && ReviBot.openingBook.TryGetBookMove(board, out string moveString))
        {
            UnityEngine.Debug.Log($"Book Move Found: {moveString}");
            Move m = board.GetMove(moveString);
            GUIHandler.UpdateBotUI(m, 0, 0, 0, 0, 0, TimeSpan.Zero);
            return m;
        }
        else if (searchSettings.openBookMode >= 0 && ReviBot.openingBook.TryGetBookMoveWeighted(board, out string weightMoveString, (double)searchSettings.openBookMode / 4))
        {
            UnityEngine.Debug.Log($"Book Move Found: {weightMoveString}");
            Move m = board.GetMove(weightMoveString);
            GUIHandler.UpdateBotUI(m, 0, 0, 0, 0, 0, TimeSpan.Zero);
            return m;
        }

        Search(searchSettings.searchDepth, 0, -double.MaxValue, double.MaxValue, 0);

        searchDiagnostics.timeTaken = s.Elapsed.TotalSeconds;
        UnityEngine.Debug.Log($"Revi Bot 2 Search Complete - Depth {searchSettings.searchDepth}\nMoves Searched: {searchDiagnostics.movesSearched}, Time Taken: {searchDiagnostics.timeTaken}");

        s.Reset();

        return searchData.bestMove;
    }

    public static double Search(int plyRemaining, int plyFroomRoot, double alpha, double beta, int extensionCount)
    {
        if (plyFroomRoot > 0) //if a move has been made from og pos
        {
            //games finished in some way, no more moves to evaluate
            if (board.state.gameState != 0) return Evaluation.RelativeEvaluate(board, Move.NullMove);

            //skip position if shorter mate found, done by checking depth 
            alpha = Math.Max(alpha, -forcedMateScore + plyFroomRoot);
            beta = Math.Max(beta, forcedMateScore - plyFroomRoot);
            if (alpha >= beta) return alpha;
        }

        // add transposition table here [NOT YET IMPLEMENTED]

        if (plyRemaining == 0) //search finished
        {
            double evaluation = QuiescenceSearch(alpha, beta, 3);
            return evaluation;
        }

        List<Move> moves = MoveOrdering.OrderedMoves(board);

        Move posBestMove = Move.NullMove;

        for (int i = 0; i < moves.Count; i++)
        {
            searchDiagnostics.movesSearched++;

            Move move = moves[i];
            int pieceType = Piece.AbsoluteType(board.board[moves[i].startPos]);
            int capturePieceType = Piece.AbsoluteType(board.board[moves[i].endPos]);

            if (plyFroomRoot == 0) searchData.firstMoveInSearchBranch = moves[i]; //used in eval function

            board.MakeMove(moves[i]);

            int extension = 0; //extend search in key moves
            if (extensionCount < MaxExtensions)
            {
                if (board.state.isCheck) extension = 1;
                else if (pieceType == Piece.PawnPiece && (Piece.Rank(move.endPos) == 0 || Piece.Rank(move.endPos) == 7))
                {
                    extension = 1;
                }
            }

            bool fullSearchRequired = true;
            double eval = 0;

            //reduce depth of search as unlike to be best move, and nothing important is happening
            if (extension == 0 && plyRemaining >= 2 && i >= 3 && capturePieceType == 0)
            {
                //2 instead of 1 as we are reducing search depth
                eval = -Search(plyRemaining - 2, plyFroomRoot + 1, -alpha - 1, -alpha, extensionCount);

                //actually a good move so lets search
                fullSearchRequired = eval > alpha;
            }

            if (fullSearchRequired)
            {
                eval = -Search(plyRemaining - 1, plyFroomRoot + 1, -beta, -alpha, extensionCount + extension);
            }
            board.UndoMove();

            //moves to good so opponent wont choose this path
            if (eval >= beta)
            {
                return beta;
            }

            //new best move
            if (eval >= alpha)
            {
                alpha = eval;
                posBestMove = moves[i];

                //top move
                if (plyFroomRoot == 0)
                {
                    searchData.bestMove = moves[i];
                    searchData.bestEval = eval;
                }
            }
        }

        return alpha;
    }

    public static double QuiescenceSearch(double alpha, double beta, int maxExtension)
    {
        double eval = Evaluation.RelativeEvaluate(board, searchData.firstMoveInSearchBranch);

        if (maxExtension == 0) return eval;

        //position checks without capturing
        if (eval >= beta)
        {
            return beta; //beta cutoff
        }

        if (eval > alpha)

        {
            alpha = eval;
        }

        List<Move> moves = MoveOrdering.OrderedMoves(board);
        for (int i = 0; i < moves.Count; i++)
        {
            if (board.board[moves[i].endPos] == 0) continue; //only looking for captures

            searchDiagnostics.movesSearched++;
            board.MakeMove(moves[i]);
            eval = -QuiescenceSearch(-beta, -alpha, maxExtension - 0);
            board.UndoMove();

            if (eval >= beta)
            {
                return beta; //beta cutoff
            }

            if (eval > alpha)
            {
                alpha = eval;
            }
        }

        return alpha;
    }
}

/// <summary> Holds settings for a bot search. [WIP] </summary>
public struct BotSearchSettings
{
    public bool useDynamicDepth;
    public int searchDepth;
    public int openBookMode;

    //-1 is best move open book
    public BotSearchSettings(int searchDepth, bool useDynamicDepth, int openBookMode = -1)
    {
        this.searchDepth = searchDepth;
        this.useDynamicDepth = useDynamicDepth;
        this.openBookMode = openBookMode;
    }
}

public struct BotSearchData
{
    public Move bestMove;
    public double bestEval;

    public Move firstMoveInSearchBranch;

    public BotSearchData(BotSearchSettings settings)
    {
        bestMove = Move.NullMove;
        bestEval = -double.MaxValue;

        firstMoveInSearchBranch = Move.NullMove;
    }
}

public struct BotSearchDiagnostics
{
    public int movesSearched;
    public double timeTaken;
}
