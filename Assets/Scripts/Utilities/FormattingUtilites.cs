using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary> Useful functions for converting to and from common notations. </summary>
public static class FormattingUtillites
{
    // --- FEN ---

    static readonly Dictionary<char, byte> fenPieceLookup = new Dictionary<char, byte>() {
        {'K', 1}, {'Q', 2}, {'R', 3}, {'B', 4}, {'N', 5}, {'P', 6},
        {'k', 7}, {'q', 8}, {'r', 9}, {'b', 10}, {'n', 11}, {'p', 12},
    };

    static readonly Dictionary<byte, char> reversedFenPieceLookup = new Dictionary<byte, char>() {
    {1, 'K'}, {2, 'Q'}, {3, 'R'}, {4, 'B'}, {5, 'N'}, {6, 'P'},
    {7, 'k'}, {8, 'q'}, {9, 'r'}, {10, 'b'}, {11, 'n'}, {12, 'p'},
    };

    /// <summary> Returns a fen string, excluding turn data, and ommiting en passant data for a -.</summary>
    public static string SimplifiedFenString(string fen)
    {
        string fenA = fen.Substring(0, fen.LastIndexOf(' '));
        fenA = fenA.Substring(0, fenA.LastIndexOf(' '));
        return fenA.Substring(0, fenA.LastIndexOf(' ')) + " -";
    }

    /// <summary> Simple check, stopping very invalid strings, use for user protection not to guarantee a valid fen. </summary>
    public static bool FenValid(string fen)
    {
        string[] splitFen = fen.Split(' ');

        if (splitFen.Length != 6) return false;
        if (splitFen[0].Count(c => c == '/') != 7) return false;

        return true;
    }

    /// <summary> Generates a board from a given fen position. </summary>
    public static Board BoardFromFenString(string fen)
    {
        Board board = new Board();

        string[] splitFen = fen.Split(' ');

        int boardCol = 0;
        int boardRow = 7;

        foreach (char c in splitFen[0])
        {
            if (c == '/') //if new line
            {
                boardCol = 0;
                boardRow--;

                if (boardRow < 0)
                {
                    break;
                }
            }
            else if (int.TryParse(c.ToString(), out int cResult)) //skip this many forward
            {
                boardCol += cResult;
            }
            else
            {
                if (!fenPieceLookup.ContainsKey(c) || boardCol > 7) continue; //some random invalid char 

                board.board[boardRow * 8 + boardCol] = fenPieceLookup[c];
                boardCol++;
            }
        }

        if (splitFen.Length > 2) //castling updates
        {
            board.state.castleRights = (byte)(splitFen[2].Contains('Q') ? 0 : 8);
            board.state.castleRights += (byte)(splitFen[2].Contains('q') ? 0 : 4);
            board.state.castleRights += (byte)(splitFen[2].Contains('K') ? 0 : 2);
            board.state.castleRights += (byte)(splitFen[2].Contains('k') ? 0 : 1);
        }
        if (splitFen.Length > 3 && splitFen[3][0] != '-') //enpassant
        {
            board.state.enPassantFile = (byte)Piece.File(PosFromBoardCode(splitFen[3]));
        }
        if (splitFen.Length > 4) board.turn = int.Parse(splitFen[4]); //turn

        board.state.zobristKey = Zobrist.CalculateZobristKey(board);
        board.previousPositions.Add(board.state.zobristKey);

        board.possibleMoves = MoveGeneration.GenerateMoves(board);

        return board;
    }

    /// <summary> Generates the fen position for a given board. </summary>
    public static string BoardToFenString(Board board)
    {
        string fenPosition = "";

        int space = 0;

        for (int y = 7; y >= 0; y--)
        {
            for (int x = 0; x <= 7; x++)
            {
                if (board.board[y * 8 + x] == 0) space++;
                else
                {
                    if (space != 0) fenPosition += space.ToString();
                    space = 0;
                    fenPosition += reversedFenPieceLookup[board.board[y * 8 + x]];
                }
            }
            if (space != 0)
            {
                fenPosition += space.ToString();
                space = 0;
            }
            if (y != 0) fenPosition += "/";
        }

        fenPosition += board.whiteTurn ? " w " : " b ";
        if (board.state.castleRights == 15) fenPosition += "-";
        else
        {
            if (!BinaryUtilities.ByteContains(board.state.castleRights, 2)) fenPosition += "K";
            if (!BinaryUtilities.ByteContains(board.state.castleRights, 0)) fenPosition += "Q";
            if (!BinaryUtilities.ByteContains(board.state.castleRights, 3)) fenPosition += "k";
            if (!BinaryUtilities.ByteContains(board.state.castleRights, 1)) fenPosition += "q";
        }

        if (board.state.enPassantFile == 0) fenPosition += " - ";
        else
        {
            fenPosition += $" {BoardCode(board.previousMoves.Peek().endPos + (board.whiteTurn ? 8 : -8))} ";
        }

        fenPosition += $"{board.turn} ";
        fenPosition += $"{board.turn / 2 + 1}";

        return fenPosition;
    }

    // --- PGN ---

    /// <summary> Generates pgn for given board [NOT IMPLEMENTED] </summary>
    public static string BoardToPgnString(Board finalBoard)
    {
        return "PGN Support Not Yet Implemented :(";
    }

    // --- BOARD ---

    /// <summary> Returns code of index on board (e.g 0 -> a1). </summary>
    public static string BoardCode(int index)
    {
        return $"{(char)(Piece.File(index) + 97)}{Piece.Rank(index) + 1}";
    }

    /// <summary> Returns index from board code (e.g 0 -> a1). </summary>
    public static int PosFromBoardCode(string notation)
    {
        return (int.Parse(notation[1].ToString()) - 1) * 8 + (notation[0] - 97);
    }

    // --- WORLD ---

    //gets index position in world
    public static Vector2 GetWorldPos(int index)
    {
        return new Vector2(index % 8 - 3.5f, index / 8 - 3.5f);
    }

    //get index from world position
    public static byte GetIndexPos(Vector2 worldPos)
    {
        return (byte)(Math.Floor(worldPos.x) + Math.Floor(worldPos.y) * 8);
    }
}
