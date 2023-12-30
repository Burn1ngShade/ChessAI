using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

//the logic for a board state in chess game
public class Board
{
    public static string defaultFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w QKqk - 0 1";
    //public static string defaultFen = "4k/pppppppp/8/8/8/8/PPPPPPPP/4K w - - 0 1";

    //actual board values do not modify this pretty please!!!!
    public byte[] board = new byte[64];

    public int gameProgress = 0;

    public byte whiteKingPos;
    public byte blackKingPos;
    public byte friendlyKingPos => whiteTurn ? whiteKingPos : blackKingPos;
    public byte oppKingPos => whiteTurn ? blackKingPos : whiteKingPos;

    public int wLastKingMove = 0;
    public int bLastKingMove = 0;

    public byte fiftyMoveRule = 100; //fifty moves = 100 player moves
    public byte castleRights = 0b0000;
    public byte enPassantFile = 0;

    public ulong pieceBitboard = 0;
    public ulong wPieceBitboard = 0;
    public ulong bPieceBitboard = 0;

    public ulong attackBitboard = 0;
    public ulong wAttackBitboard = 0;
    public ulong bAttackBitboard = 0;
    public ulong oppAttackBitboard => whiteTurn ? bAttackBitboard : wAttackBitboard;

    public int legalMoves = 0;
    public int legalQueenMoves = 0;
    public int oppLegalMoves = 0;
    public int oppLegalQueenMoves = 0;

    //check stuff
    public ulong pinBitboard = 0;
    public ulong checkBitboard = 0;
    public ulong kingBlockerBitboard = 0;

    public bool majorCapture = false;

    public List<Move> moves = new List<Move>();
    public Stack<Move> previousMoves = new Stack<Move>();

    HashSet<ulong> previousPositions = new HashSet<ulong>();
    HashSet<ulong> doublePreviousPositions = new HashSet<ulong>(); //previous positions that have occured twice
    public ulong zobristKey;

    //game data
    public int turn { get; private set; } = 0;
    public bool whiteTurn => turn % 2 == 0;

    static readonly Dictionary<char, byte> fenPieceLookup = new Dictionary<char, byte>() {
        {'K', 1}, {'Q', 2}, {'R', 3}, {'B', 4}, {'N', 5}, {'P', 6},
        {'k', 7}, {'q', 8}, {'r', 9}, {'b', 10}, {'n', 11}, {'p', 12},
    };

    static readonly Dictionary<byte, char> reversedFenPieceLookup = new Dictionary<byte, char>() {
    {1, 'K'}, {2, 'Q'}, {3, 'R'}, {4, 'B'}, {5, 'N'}, {6, 'P'},
    {7, 'k'}, {8, 'q'}, {9, 'r'}, {10, 'b'}, {11, 'n'}, {12, 'p'},
    };

    //constructors 
    public Board(string fenPosition)
    {
        string[] splitFen = fenPosition.Split(' ');

        int boardCol = 0;
        int boardRow = 7;

        foreach (char c in splitFen[0])
        {
            if (c == '/')
            {
                boardCol = 0;
                boardRow--;

                if (boardRow < 0)
                {
                    break;
                }
            }
            else if (int.TryParse(c.ToString(), out int cResult))
            {
                boardCol += cResult;
            }
            else
            {
                if (!fenPieceLookup.ContainsKey(c) || boardCol > 7) continue;

                board[boardRow * 8 + boardCol] = fenPieceLookup[c];
                boardCol++;
            }
        }

        if (splitFen.Length > 2)
        {
            castleRights = (byte)(splitFen[2].Contains('Q') ? 0 : 8);
            castleRights += (byte)(splitFen[2].Contains('q') ? 0 : 4);
            castleRights += (byte)(splitFen[2].Contains('K') ? 0 : 2);
            castleRights += (byte)(splitFen[2].Contains('k') ? 0 : 1);
        }
        if (splitFen.Length > 3 && splitFen[3][0] != '-')
        {
            enPassantFile = byte.Parse(splitFen[3]);
        }
        if (splitFen.Length > 4) turn = int.Parse(splitFen[4]);

        zobristKey = Zobrist.CalculateZobristKey(this);
        previousPositions.Add(zobristKey);

        this.moves = MoveGeneration.GenerateMoves(this);
    }

    public Board(Board b)
    {
        Array.Copy(b.board, board, 64);

        gameProgress = b.gameProgress;

        whiteKingPos = b.whiteKingPos;
        blackKingPos = b.blackKingPos;

        castleRights = b.castleRights;
        fiftyMoveRule = b.fiftyMoveRule;
        enPassantFile = b.enPassantFile;

        wPieceBitboard = b.wPieceBitboard;
        bPieceBitboard = b.bPieceBitboard;

        attackBitboard = b.attackBitboard;
        wAttackBitboard = b.wAttackBitboard;
        bAttackBitboard = b.bAttackBitboard;

        pinBitboard = b.pinBitboard;
        kingBlockerBitboard = b.kingBlockerBitboard;
        checkBitboard = b.checkBitboard;

        zobristKey = b.zobristKey;

        for (int i = 0; i < b.moves.Count; i++) moves.Add(new Move(b.moves[i]));
        previousMoves = new Stack<Move>(b.previousMoves.Select(item => new Move(item)));
        previousPositions = new HashSet<ulong>(b.previousPositions);
        doublePreviousPositions = new HashSet<ulong>(b.doublePreviousPositions);

        turn = b.turn;
    }

    public string ToFenPosition()
    {
        string fenPosition = "";

        int space = 0;

        for (int y = 7; y >= 0; y--)
        {
            for (int x = 0; x <= 7; x++)
            {
                if (board[y * 8 + x] == 0) space++;
                else
                {
                    if (space != 0) fenPosition += space.ToString();
                    space = 0;
                    fenPosition += reversedFenPieceLookup[board[y * 8 + x]];
                }
            }
            if (space != 0)
            {
                fenPosition += space.ToString();
                space = 0;
            }
            if (y != 0) fenPosition += "/";
        }

        return fenPosition;
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

    public List<Move> GetPlayerMoves()
    {
        List<Move> m = new List<Move>();

        int maxValue = 1;

        int value;
        for (int i = 0; i < moves.Count; i++)
        {
            if (Piece.IsWhite(board[moves[i].startPos]) != whiteTurn) continue;

            value = Piece.EvalValue(board[moves[i].endPos]);

            if (maxValue <= value)
            {
                maxValue = value;
                m.Insert(0, moves[i]);
            }
            if (Piece.IsWhite(board[moves[i].startPos]) == whiteTurn) m.Add(moves[i]);
        }

        return m;
    }

    public Move GetMove(string move)
    {
        move = move.Replace(" ", "");

        string startPos = move.Substring(0, 2);
        string endPos = move.Substring(2, 2);

        List<Move> moves = GetPlayerMoves();
        for (int i = 0; i < moves.Count; i++)
        {
            if (Piece.AlgebraicNotation(moves[i].startPos) == startPos && Piece.AlgebraicNotation(moves[i].endPos) == endPos)
            {
                return moves[i];
            }
        }
        Debug.Log(startPos + " : " + endPos);
        Debug.Log(move + " : " + Piece.AlgebraicNotation(moves[0].startPos) + " : " + Piece.AlgebraicNotation(moves[0].endPos));

        return null;
    }

    public bool MakeMove(Move move)
    {
        move.SetPieceAndCapturePiece(this);

        board[move.endPos] = move.piece;
        board[move.startPos] = 0;

        bool isWhite = Piece.IsWhite(move.piece);
        int offset = isWhite ? 0 : 56;

        if (move.piece == 7) bLastKingMove = turn + 1;
        else if (move.piece == 1) wLastKingMove = turn + 1;

        if (Piece.AbsoluteType(move.piece) == 6 && Math.Abs(move.startPos - move.endPos) == 16) enPassantFile = (byte)(move.startPos % 8 + 1);
        else enPassantFile = 0;

        //castling rights

        if (move.startPos - offset == 4)
        {
            castleRights = (byte)(castleRights | (isWhite ? 0b00000101 : 0b00001010));
        }
        else if (move.startPos - offset == 0)
        {
            castleRights = (byte)(castleRights | (isWhite ? 0b00000001 : 0b00000010));
        }
        else if (move.startPos - offset == 7)
        {
            castleRights = (byte)(castleRights | (isWhite ? 0b00000100 : 0b00001000));
        }

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

        turn++;

        if (Piece.AbsoluteType(move.piece) == 6 || move.capturePiece != 0) fiftyMoveRule = 100;
        else fiftyMoveRule--;

        zobristKey = Zobrist.CalculateZobristKey(this);

        previousMoves.Push(move);

        this.moves = MoveGeneration.GenerateMoves(this);
        UpdateGameProgress();

        if (previousPositions.Contains(zobristKey))
        {
            if (!doublePreviousPositions.Contains(zobristKey)) doublePreviousPositions.Add(zobristKey);
        }
        else previousPositions.Add(zobristKey);

        return true;
    }

    //undo the most recent move
    public bool UndoMove()
    {
        if (previousMoves.Count == 0) return false;

        Move move = previousMoves.Pop();

        board[move.startPos] = move.piece;
        board[move.endPos] = move.capturePiece;

        castleRights = move.castleRights;
        fiftyMoveRule = move.fiftyMoveRule;
        enPassantFile = move.enPassantFile;

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

        bLastKingMove = move.bLastKingMove;
        wLastKingMove = move.wLastKingMove;

        this.moves = MoveGeneration.GenerateMoves(this);

        if (gameProgress != 5)
        {
            if (doublePreviousPositions.Contains(zobristKey)) doublePreviousPositions.Remove(zobristKey);
            else previousPositions.Remove(zobristKey);
        }

        zobristKey = move.zobristKey;

        //Debug.Log(zobristKey + " : " + previousPositions.Count + " : " + doublePreviousPositions.Count);

        gameProgress = 0;

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



    public void UpdateGameProgress()
    {
        if (doublePreviousPositions.Contains(zobristKey))
        {
            gameProgress = 5;
            return;
        }

        if (GUIHandler.legalMoves == 0)
        {
            if (InCheck()) gameProgress = whiteTurn ? 2 : 1;
            else gameProgress = 3;
        }
        else if (fiftyMoveRule == 0)
        {
            gameProgress = 4;
        }
    }
}

