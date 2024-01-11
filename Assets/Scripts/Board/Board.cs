using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;

/// <summary> Class representing a state of a chess game. </summary>
public class Board
{
    public static string usedFen => DefaultFen; //position loaded by chess engine

    public const string DefaultFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w QKqk - 0 1"; //default starting pos in chess
    public const string KingPawnTestFen = "8/2KP/8/8/8/8/8/kq b - - 1 1";
    public const string WhiteKingPawnTestFen = "KQ/8/8/8/8/8/2kp/8 w - - 0 1";
    public const string RookTestFen = "RK/8/8/8/k/8/8/8 w - - 0 1";

    public byte[] board = new byte[64]; //actual values of board

    //bitboard for piece locations
    public ulong pieceBitboard = 0;
    public ulong wPieceBitboard = 0;
    public ulong bPieceBitboard = 0;

    //bitboard for piece attacks
    public ulong attackBitboard = 0;
    public ulong wAttackBitboard = 0;
    public ulong bAttackBitboard = 0;

    //board info
    public (int friendlyAll, int friendlyQueen, int oppAll, int oppQueen) legalMoves;

    public int turn = 0;
    public BoardState state = new BoardState();

    //useful shorthand
    public byte friendlyKingPos => whiteTurn ? state.whiteKingPos : state.blackKingPos;
    public ulong oppAttackBitboard => whiteTurn ? bAttackBitboard : wAttackBitboard;
    public bool whiteTurn => turn % 2 == 0;

    //check stuff
    public ulong pinBitboard = 0;
    public ulong checkBitboard = 0;
    public ulong kingBlockerBitboard = 0;

    //attackbitboard + pawn attacks + defended pieces
    public ulong wPossbileAttackBitboard = 0;
    public ulong bPossbileAttackBitboard = 0;

    public ulong wPawnAttack = 0;
    public ulong bPawnAttack = 0;

    public List<Move> possibleMoves = new List<Move>(); //current moves in position
    public Stack<Move> previousMoves = new Stack<Move>(); //past moves in game

    public HashSet<ulong> previousPositions = new HashSet<ulong>();
    public HashSet<ulong> doublePreviousPositions = new HashSet<ulong>(); //previous positions that have occured twice

    //constructors 

    public Board() { }

    public Board(string fen) : this(FormattingUtillites.BoardFromFenString(fen)) { }

    public Board(Board b)
    {
        Array.Copy(b.board, board, 64);

        state.whiteKingPos = b.state.whiteKingPos;
        state.blackKingPos = b.state.blackKingPos;

        pieceBitboard = b.pieceBitboard;
        wPieceBitboard = b.wPieceBitboard;
        bPieceBitboard = b.bPieceBitboard;

        attackBitboard = b.attackBitboard;
        wAttackBitboard = b.wAttackBitboard;
        bAttackBitboard = b.bAttackBitboard;

        pinBitboard = b.pinBitboard;
        kingBlockerBitboard = b.kingBlockerBitboard;
        checkBitboard = b.checkBitboard;

        wPossbileAttackBitboard = b.wPossbileAttackBitboard;
        bPossbileAttackBitboard = b.bPossbileAttackBitboard;

        wPawnAttack = b.wPawnAttack;
        bPawnAttack = b.bPawnAttack;

        turn = b.turn;
        state = new BoardState(b.state);

        for (int i = 0; i < b.possibleMoves.Count; i++) possibleMoves.Add(new Move(b.possibleMoves[i]));
        previousMoves = new Stack<Move>(b.previousMoves.Select(item => new Move(item)));
        previousPositions = new HashSet<ulong>(b.previousPositions);
        doublePreviousPositions = new HashSet<ulong>(b.doublePreviousPositions);
    }

    // --- MOVES ---

    /// <summary> Get equiv move on board with additional data. </summary>
    public Move GetMove(Move move)
    {
        if (!possibleMoves.Select(m => m.GetHashCode()).ToList().Contains(move.GetHashCode())) return null;
        return possibleMoves.Find(m => m.GetHashCode() == move.GetHashCode()); //so it has type data 
    }

    /// <summary> Get move on board from given board code. </summary>
    public Move GetMove(string move)
    {
        move = move.Replace(" ", "");

        string startPos = move.Substring(0, 2);
        string endPos = move.Substring(2, 2);

        for (int i = 0; i < possibleMoves.Count; i++)
        {
            if (FormattingUtillites.BoardCode(possibleMoves[i].startPos) == startPos && FormattingUtillites.BoardCode(possibleMoves[i].endPos) == endPos)
            {
                return possibleMoves[i];
            }
        }

        return null;
    }

    /// <summary> Gets all moves for piece at given index. </summary>
    public List<Move> GetPieceMoves(byte index)
    {
        List<Move> legalMoves = new List<Move>();

        for (int i = 0; i < possibleMoves.Count; i++)
        {
            if (possibleMoves[i].startPos == index) legalMoves.Add(possibleMoves[i]);
        }

        return legalMoves;
    }

    /// <summary> Make a move in given chess position. </summary>
    public bool MakeMove(Move move)
    {
        move.SetMoveInfo(this);

        board[move.endPos] = move.piece;
        board[move.startPos] = 0;

        bool isWhite = Piece.IsWhite(move.piece);
        int offset = isWhite ? 0 : 56;

        if (move.capturePiece != 0) state.zobristKey ^= Zobrist.piecesArray[move.capturePiece - 1, move.endPos]; //remove capture piece

        state.zobristKey ^= Zobrist.enPassantFile[state.enPassantFile];

        if (Piece.AbsoluteType(move.piece) == 6 && Math.Abs(move.startPos - move.endPos) == 16) state.enPassantFile = (byte)(move.startPos % 8 + 1);
        else state.enPassantFile = 0;

        state.zobristKey ^= Zobrist.enPassantFile[state.enPassantFile];

        //castling rights

        state.zobristKey ^= Zobrist.castlingRights[state.castleRights];

        if (move.startPos - offset == 4)
        {
            state.castleRights = (byte)(state.castleRights | (isWhite ? 0b00000101 : 0b00001010));
        }
        else if (move.startPos - offset == 0)
        {
            state.castleRights = (byte)(state.castleRights | (isWhite ? 0b00000001 : 0b00000010));
        }
        else if (move.startPos - offset == 7)
        {
            state.castleRights = (byte)(state.castleRights | (isWhite ? 0b00000100 : 0b00001000));
        }

        state.zobristKey ^= Zobrist.castlingRights[state.castleRights];

        if (move.type == 1)
        {
            state.zobristKey ^= Zobrist.piecesArray[board[move.endPos + 8 * (isWhite ? -1 : 1)] - 1, move.endPos + 8 * (isWhite ? -1 : 1)];
            board[move.endPos + 8 * (isWhite ? -1 : 1)] = 0;
        }
        else if (move.type >= 2 && move.type <= 5)
        {
            state.zobristKey ^= Zobrist.piecesArray[move.piece - 1, move.endPos];
            board[move.endPos] = (byte)(move.type + (isWhite ? 0 : 6));
            state.zobristKey ^= Zobrist.piecesArray[(byte)move.type + (isWhite ? -1 : 5), move.endPos];
        }
        else if (move.type == 6)
        {
            state.zobristKey ^= Zobrist.piecesArray[2 + (isWhite ? 0 : 6), move.startPos - 4];
            state.zobristKey ^= Zobrist.piecesArray[2 + (isWhite ? 0 : 6), move.startPos - 1];
            board[move.startPos - 4] = 0;
            board[move.startPos - 1] = (byte)(3 + (isWhite ? 0 : 6));
        }
        else if (move.type == 7)
        {
            state.zobristKey ^= Zobrist.piecesArray[2 + (isWhite ? 0 : 6), move.startPos + 3];
            state.zobristKey ^= Zobrist.piecesArray[2 + (isWhite ? 0 : 6), move.startPos + 1];
            board[move.startPos + 3] = 0;
            board[move.startPos + 1] = (byte)(3 + (isWhite ? 0 : 6));
        }

        turn++;

        state.zobristKey ^= Zobrist.sideToMove;
        if (move.piece != 0) state.zobristKey ^= Zobrist.piecesArray[move.piece - 1, move.startPos];
        if (move.piece != 0) state.zobristKey ^= Zobrist.piecesArray[move.piece - 1, move.endPos];

        if (Piece.AbsoluteType(move.piece) == 6 || move.capturePiece != 0) state.fiftyMoveRule = 100;
        else state.fiftyMoveRule--;

        previousMoves.Push(move);

        this.possibleMoves = MoveGeneration.GenerateMoves(this);
        UpdateGameState();

        if (previousPositions.Contains(state.zobristKey))
        {
            if (!doublePreviousPositions.Contains(state.zobristKey)) doublePreviousPositions.Add(state.zobristKey);
        }
        else previousPositions.Add(state.zobristKey);

        return true;
    }

    /// <summary> Undoes most recent move in chess game. </summary>
    public bool UndoMove()
    {
        if (previousMoves.Count == 0) return false;

        Move move = previousMoves.Pop();

        board[move.startPos] = move.piece;
        board[move.endPos] = move.capturePiece;

        if (move.type == 1)
        {
            board[move.endPos + 8 * (Piece.IsWhite(move.piece) ? -1 : 1)] = (byte)(6 + (Piece.IsWhite(move.piece) ? 6 : 0));
        }
        else if (move.type == 6)
        {
            board[move.startPos - 4] = (byte)(3 + (Piece.IsWhite(move.piece) ? 0 : 6));
            board[move.startPos - 1] = 0;
        }
        else if (move.type == 7)
        {
            board[move.startPos + 3] = (byte)(3 + (Piece.IsWhite(move.piece) ? 0 : 6));
            board[move.startPos + 1] = 0;
        }

        turn--;

        if (state.gameState != 5)
        {
            if (doublePreviousPositions.Contains(state.zobristKey)) doublePreviousPositions.Remove(state.zobristKey);
            else previousPositions.Remove(state.zobristKey);
        }

        state = new BoardState(move.state);

        this.possibleMoves = MoveGeneration.GenerateMoves(this);

        state.gameState = 0;

        return true;
    }


    /// <summary> Updates current state of game (playing, won, drawn). </summary>
    public void UpdateGameState()
    {
        if (doublePreviousPositions.Contains(state.zobristKey))
        {
            state.gameState = 5;
            return;
        }

        if (GUIHandler.legalMoves == 0)
        {
            if (state.isCheck) state.gameState = whiteTurn ? 2 : 1;
            else state.gameState = 3;
        }
        else if (state.fiftyMoveRule == 0)
        {
            state.gameState = 4;
        }
    }
}

/// <summary> Represents attributes of current board position. </summary>
public class BoardState
{
    //fifty moves = 100 ply (what 'turn' tracks)
    public byte fiftyMoveRule;
    public byte castleRights;
    public byte enPassantFile;

    public ulong zobristKey;
    public int gameState;

    public bool isCheck;
    public bool isCaptureOrPromotion;

    public byte whiteKingPos;
    public byte blackKingPos;

    public BoardState()
    {
        fiftyMoveRule = 100;
    }

    public BoardState(BoardState state)
    {
        fiftyMoveRule = state.fiftyMoveRule;
        castleRights = state.castleRights;
        enPassantFile = state.enPassantFile;

        zobristKey = state.zobristKey;
        gameState = state.gameState;

        isCheck = state.isCheck;
        isCaptureOrPromotion = state.isCaptureOrPromotion;

        whiteKingPos = state.whiteKingPos;
        blackKingPos = state.blackKingPos;
    }
}
