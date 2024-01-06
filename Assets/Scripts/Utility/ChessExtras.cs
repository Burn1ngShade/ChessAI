using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ChessExtras
{
    // FEN stuff

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

    public static Board BoardFromFenString(string fen)
    {
        Board board = new Board();

        string[] splitFen = fen.Split(' ');

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

                board.board[boardRow * 8 + boardCol] = fenPieceLookup[c];
                boardCol++;
            }
        }

        if (splitFen.Length > 2)
        {
            board.state.castleRights = (byte)(splitFen[2].Contains('Q') ? 0 : 8);
            board.state.castleRights += (byte)(splitFen[2].Contains('q') ? 0 : 4);
            board.state.castleRights += (byte)(splitFen[2].Contains('K') ? 0 : 2);
            board.state.castleRights += (byte)(splitFen[2].Contains('k') ? 0 : 1);
        }
        if (splitFen.Length > 3 && splitFen[3][0] != '-')
        {
            board.state.enPassantFile = (byte)Piece.File(Piece.PosFromAlgebraicNotation(splitFen[3]));
        }
        if (splitFen.Length > 4) board.turn = int.Parse(splitFen[4]);

        board.state.zobristKey = Zobrist.CalculateZobristKey(board);
        board.previousPositions.Add(board.state.zobristKey);

        board.moves = MoveGeneration.GenerateMoves(board);

        return board;
    }

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
            if (!BinaryExtras.ByteContains(board.state.castleRights, 2)) fenPosition += "K";
            if (!BinaryExtras.ByteContains(board.state.castleRights, 0)) fenPosition += "Q";
            if (!BinaryExtras.ByteContains(board.state.castleRights, 3)) fenPosition += "k";
            if (!BinaryExtras.ByteContains(board.state.castleRights, 1)) fenPosition += "q";
        }

        if (board.state.enPassantFile == 0) fenPosition += " - ";
        else
        {
            fenPosition += $" {Piece.AlgebraicNotation(board.previousMoves.Peek().endPos + (board.whiteTurn ? 8 : -8))} ";
        }

        fenPosition += $"{board.turn} ";
        fenPosition += $"{board.turn / 2 + 1}";

        return fenPosition;
    }
}
