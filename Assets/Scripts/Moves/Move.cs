using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

//the logic for a move on a board 
public class Move
{
    public byte startPos; //pos move start
    public byte endPos; //pos move end

    public byte piece { get; private set; }
    public byte capturePiece { get; private set; }

    public byte type; //0 normal, 1 enpassanent, 2-5 promotion, 6-7 castle

    public byte castleRights;
    public byte fiftyMoveRule;
    public byte enPassantFile;

    public int wLastKingMove;
    public int bLastKingMove;

    public ulong zobristKey = 0;

    public void SetPieceAndCapturePiece(Board board) //shouldnt really need calls outside of Board
    {
        piece = board.board[startPos];
        capturePiece = board.board[endPos];

        castleRights = board.castleRights;
        fiftyMoveRule = board.fiftyMoveRule;
        enPassantFile = board.enPassantFile;

        wLastKingMove = board.wLastKingMove;
        bLastKingMove = board.bLastKingMove;

        zobristKey = board.zobristKey;
    }

    public Move(Move move)
    {
        this.startPos = move.startPos;
        this.endPos = move.endPos;

        this.piece = move.piece;
        this.capturePiece = move.capturePiece;

        this.type = move.type;

        this.capturePiece = move.capturePiece;
        this.fiftyMoveRule = move.fiftyMoveRule;
    }

    public Move(byte startPos, byte endPos, byte type = 0)
    {
        this.startPos = startPos;
        this.endPos = endPos;
        this.type = type;
    }

    public override int GetHashCode()
    {
        return startPos << 8 | endPos; //should be unique for every move?
    }

    public override string ToString()
    {
        return $"{startPos} : {endPos}";
    }
}
