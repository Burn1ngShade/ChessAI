using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TMPro.EditorUtilities;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;

//the logic for a board state in chess game
public class Board
{
    //public static string usedFen => defaultFen;
    public static string usedFen => DefaultFen; //position loaded by chess engine

    public const string DefaultFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w QKqk - 0 1"; //default starting pos in chess
    public const string KingPawnTestFen = "8/2KP/8/8/8/8/8/kq b - - 1 1";
    public const string WhiteKingPawnTestFen = "KQ/8/8/8/8/8/2kp/8 w - - 0 1";
    public const string RookTestFen = "RK/8/8/8/k/8/8/8 w - - 0 1";

    public byte[] board = new byte[64]; //actual values of board

    //public int gameProgress = 0; //state of game, 0 = playing, 1 = white win, ect.

    public byte whiteKingPos;
    public byte blackKingPos;
    public byte friendlyKingPos => whiteTurn ? whiteKingPos : blackKingPos;
    public byte oppKingPos => whiteTurn ? blackKingPos : whiteKingPos;

    public ulong pieceBitboard = 0;
    public ulong wPieceBitboard = 0;
    public ulong bPieceBitboard = 0;

    public ulong attackBitboard = 0;
    public ulong wAttackBitboard = 0;
    public ulong bAttackBitboard = 0;
    public ulong oppAttackBitboard => whiteTurn ? bAttackBitboard : wAttackBitboard;

    public (int friendlyAll, int friendlyQueen, int oppAll, int oppQueen) legalMoves;
    public bool majorEvent;
    public bool isCheck;

    //check stuff
    public ulong pinBitboard = 0;
    public ulong checkBitboard = 0;
    public ulong kingBlockerBitboard = 0;

    public ulong wPossbileAttackBitboard = 0;
    public ulong bPossbileAttackBitboard = 0;

    public List<Move> moves = new List<Move>();
    public Stack<Move> previousMoves = new Stack<Move>();

    public HashSet<ulong> previousPositions = new HashSet<ulong>();
    public HashSet<ulong> doublePreviousPositions = new HashSet<ulong>(); //previous positions that have occured twice
    // public ulong zobristKey;

    public BoardState state = new BoardState();

    //game data
    public int turn = 0;
    public bool whiteTurn => turn % 2 == 0;

    //constructors 

    public Board() { }

    public Board(string fen) : this(ChessExtras.BoardFromFenString(fen)) { }

    public Board(Board b)
    {
        Array.Copy(b.board, board, 64);

        whiteKingPos = b.whiteKingPos;
        blackKingPos = b.blackKingPos;

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

        state = new BoardState(b.state);

        for (int i = 0; i < b.moves.Count; i++) moves.Add(new Move(b.moves[i]));
        previousMoves = new Stack<Move>(b.previousMoves.Select(item => new Move(item)));
        previousPositions = new HashSet<ulong>(b.previousPositions);
        doublePreviousPositions = new HashSet<ulong>(b.doublePreviousPositions);

        turn = b.turn;
    }

    //gets index position in world
    public static Vector2 GetWorldPos(byte index)
    {
        return new Vector2(index % 8 - 3.5f, index / 8 - 3.5f);
    }

    //get index from world position
    public static byte GetIndexPos(Vector2 worldPos)
    {
        return (byte)(Math.Floor(worldPos.x) + Math.Floor(worldPos.y) * 8);
    }

    //actual chess

    //gets move with hiden data from move of matching hash
    public Move GetMove(Move move)
    {
        if (!moves.Select(m => m.GetHashCode()).ToList().Contains(move.GetHashCode())) return null;
        return moves.Find(m => m.GetHashCode() == move.GetHashCode()); //so it has type data 
    }

    public Move GetMove(string move)
    {
        move = move.Replace(" ", "");

        string startPos = move.Substring(0, 2);
        string endPos = move.Substring(2, 2);

        for (int i = 0; i < moves.Count; i++)
        {
            if (Piece.AlgebraicNotation(moves[i].startPos) == startPos && Piece.AlgebraicNotation(moves[i].endPos) == endPos)
            {
                return moves[i];
            }
        }

        return null;
    }

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
        state.zobristKey ^= Zobrist.piecesArray[move.piece - 1, move.startPos];
        state.zobristKey ^= Zobrist.piecesArray[move.piece - 1, move.endPos];

        if (Piece.AbsoluteType(move.piece) == 6 || move.capturePiece != 0) state.fiftyMoveRule = 100;
        else state.fiftyMoveRule--;

        previousMoves.Push(move);

        this.moves = MoveGeneration.GenerateMoves(this);
        UpdateGameState();

        if (previousPositions.Contains(state.zobristKey))
        {
            if (!doublePreviousPositions.Contains(state.zobristKey)) doublePreviousPositions.Add(state.zobristKey);
        }
        else previousPositions.Add(state.zobristKey);

        return true;
    }

    //undo the most recent move
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

        this.moves = MoveGeneration.GenerateMoves(this);

        //Debug.Log(zobristKey + " : " + previousPositions.Count + " : " + doublePreviousPositions.Count);

        state.gameState = 0;

        return true;
    }

    public List<Move> GetPieceMoves(byte pieceIndex)
    {
        List<Move> legalMoves = new List<Move>();

        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].startPos == pieceIndex) legalMoves.Add(moves[i]);
        }

        return legalMoves;
    }

    public bool InCheck()
    {
        return (attackBitboard & (1UL << (whiteTurn ? whiteKingPos : blackKingPos))) != 0;
    }

    //generates moves, not accounting for checks!
    public bool SmartCheck(byte[] b, Move move)
    {
        byte[] board = new byte[64];
        Array.Copy(b, board, 64);

        byte piece = board[move.startPos];

        board[move.endPos] = piece;
        board[move.startPos] = 0;

        bool isWhite = Piece.IsWhite(piece);
        int offset = isWhite ? 0 : 56;

        if (move.type == 1)
        {
            board[move.endPos + 8 * (isWhite ? -1 : 1)] = 0;
        }
        else if (move.type >= 2 && move.type <= 5)
        {
            board[move.endPos] = (byte)(move.type + (isWhite ? 0 : 6));
        }
        else if (move.type == 6)
        {
            board[move.startPos - 4] = 0;
            board[move.startPos - 1] = (byte)(3 + (isWhite ? 0 : 6));
        }
        else if (move.type == 7)
        {
            board[move.startPos + 3] = 0;
            board[move.startPos + 1] = (byte)(3 + (isWhite ? 0 : 6));
        }

        byte whiteKingPos = Piece.AbsoluteType(piece) == 1 && isWhite ? move.endPos : this.whiteKingPos;
        byte blackKingPos = Piece.AbsoluteType(piece) == 1 && !isWhite ? move.endPos : this.blackKingPos;

        //only need to consider one side of attacks
        ulong bitboard = 0; //bit board for opposition side
        List<byte> alreadyLanded = new List<byte>(); //already been here

        Board newBoard = new Board(this);
        newBoard.board = board;

        List<Move> newMoves = MoveGeneration.GenerateMoves(newBoard);
        foreach (Move m in newMoves)
        {
            if (Piece.IsWhite(board[m.startPos]) != whiteTurn && !alreadyLanded.Contains(m.endPos))
            {
                bitboard += (1UL << m.endPos);
                alreadyLanded.Add(m.endPos);
            }
        }

        return (bitboard & (1UL << (!whiteTurn ? blackKingPos : whiteKingPos))) != 0;
        //eg white turn checking if new move puts in checkk
        //move made
        //generate all attacks for black (now there move)
        //if check no go!
    }

    public void UpdateGameState()
    {
        if (doublePreviousPositions.Contains(state.zobristKey))
        {
            state.gameState = 5;
            return;
        }

        if (GUIHandler.legalMoves == 0)
        {
            if (InCheck()) state.gameState = whiteTurn ? 2 : 1;
            else state.gameState = 3;
        }
        else if (state.fiftyMoveRule == 0)
        {
            state.gameState = 4;
        }
    }
}

/*class responsible for all data
regarding the state of the board */
public class BoardState
{
    //fifty moves = 100 ply (what 'turn' tracks)
    public byte fiftyMoveRule;
    public byte castleRights;
    public byte enPassantFile; 

    public ulong zobristKey;
    public int gameState;

    public BoardState()
    {
        this.fiftyMoveRule = 100;
        this.castleRights = 0;
        this.enPassantFile = 0;

        this.zobristKey = 0;
        this.gameState = 0;
    }

    public BoardState(BoardState state)
    {
        fiftyMoveRule = state.fiftyMoveRule;
        castleRights = state.castleRights;
        enPassantFile = state.enPassantFile;

        zobristKey = state.zobristKey;
        gameState = state.gameState;
    }
}
