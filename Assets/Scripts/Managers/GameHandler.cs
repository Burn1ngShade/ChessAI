using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;

/// <summary> Responsible for handling user and ai interaction with chess game </summary>
public class GameHandler : MonoBehaviour
{
    public static GameHandler Instance;

    public static Board board;

    public enum BotMode { Off, White, Black, Both }
    public static BotMode botMode;

    public enum GameState { Playing, Promotion, Over }
    public static GameState gameState = GameState.Playing;

    byte selectedPiece = byte.MaxValue;
    byte lastSelectedSquare = byte.MaxValue;
    public static byte selectedPromotion = byte.MaxValue;

    public static bool useDynamicDepth = true;
    public static bool inMenu = false;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Revi.openingBook = new OpeningBook(File.ReadAllText("Assets/Book.txt"));
        SetUpChessBoard(Board.usedFen);
    }

    private void Update()
    {
        UpdateDebugSettings();
        UpdateGameInteraction();
    }

    /// <summary> Updates debug settings of the chess engine. </summary>
    void UpdateDebugSettings()
    {
        if (Input.GetKeyDown(KeyCode.R)) //reload game
        {
            SetUpChessBoard(Board.usedFen);
        }
        else if (Input.GetKeyDown(KeyCode.G))
        {
            GUIHandler.showAttackBitboard = !GUIHandler.showAttackBitboard;
            GUIHandler.UpdateUI();
            if (!GUIHandler.showAttackBitboard) GUIHandler.UpdateBoardHighlights(1, GUIHandler.ClearColourHighlights());
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            GUIHandler.showPinBitboard = !GUIHandler.showPinBitboard;
            GUIHandler.UpdateUI();
            if (!GUIHandler.showPinBitboard) GUIHandler.UpdateBoardHighlights(2, GUIHandler.ClearColourHighlights());
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            GUIHandler.showPossibleAttackBitboard = !GUIHandler.showPossibleAttackBitboard;
            GUIHandler.UpdateUI();
            if (!GUIHandler.showPossibleAttackBitboard) GUIHandler.UpdateBoardHighlights(3, GUIHandler.ClearColourHighlights());
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            botMode = (BotMode)0;
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            botMode = (BotMode)1;
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            botMode = (BotMode)2;
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            botMode = (BotMode)3;
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.K))
        {
            Revi.searchDepth = Math.Clamp(Revi.searchDepth + 1, 2, 8);
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            Revi.searchDepth = Math.Clamp(Revi.searchDepth - 1, 2, 8);
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.L))
        {
            useDynamicDepth = !useDynamicDepth;
            GUIHandler.UpdateBotUINull();
        }

        GUIHandler.UpdateBoardHighlights();
    }

    /// <summary> Updates game interaction of the chess engine. </summary>
    void UpdateGameInteraction()
    {
        if (inMenu) return; 

        switch (gameState)
        {
            case GameState.Playing:
                if (board.state.gameState != 0)
                {
                    gameState = GameState.Over;
                    GUIHandler.ToggleEndGameUI(0);
                    return;
                }

                if (board.whiteTurn && (botMode == BotMode.White || botMode == BotMode.Both)) HandleBotGameplay();
                else if (!board.whiteTurn && (botMode == BotMode.Black || botMode == BotMode.Both)) HandleBotGameplay();
                else HandleGameplay();
                break;
            case GameState.Promotion:
                HandlePromotion();
                break;
            case GameState.Over:
                break;
        }
    }

    /// <summary> Handles bot gameplay. </summary>
    void HandleBotGameplay()
    {
        Move m = Revi.GetMove(board, Revi.searchDepth, useDynamicDepth);

        board.MakeMove(m);

        GUIHandler.UpdateBoardUI(new List<Move>(), GUIHandler.GenerateLastMoveHighlight());
    }

    /// <summary> Handles gameplay. </summary>
    void HandleGameplay()
    {
        if (Input.GetMouseButtonDown(0)) // Human move
        {
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition) + new Vector3(4, 4);

            if (mousePosition.x >= 0 && mousePosition.x <= 8 && mousePosition.y >= 0 && mousePosition.y <= 8)
            {
                byte index = Board.GetIndexPos(mousePosition);
                if (selectedPiece == byte.MaxValue && board.board[index] != 0 && Piece.IsWhite(board.board[index]) == board.whiteTurn)
                {
                    selectedPiece = index;
                    List<Move> moves = board.GetPieceMoves(index);
                    GUIHandler.UpdateBoardUI(moves, GUIHandler.GenerateLastMoveHighlight(index));
                }
                else if (selectedPiece != byte.MaxValue)
                {
                    lastSelectedSquare = index;
                    Move move = board.GetMove(new Move(selectedPiece, index));

                    if (move != null)
                    {
                        if (move.type >= 2 && move.type <= 5)
                        {
                            gameState = GameState.Promotion;
                            GUIHandler.TogglePromotionUI(0);
                            return;
                        }

                        board.MakeMove(move);
                    }


                    GUIHandler.UpdateBoardUI(new List<Move>(), GUIHandler.GenerateLastMoveHighlight());

                    selectedPiece = byte.MaxValue;
                }
            }
        }
    }

    /// <summary> Handles promotion. </summary>
    void HandlePromotion()
    {
        if (selectedPromotion != byte.MaxValue)
        {
            gameState = GameState.Playing;

            Move move = board.GetMove(new Move(selectedPiece, lastSelectedSquare));
            move.type = selectedPromotion;
            selectedPromotion = byte.MaxValue;

            board.MakeMove(move);

            selectedPiece = byte.MaxValue;

            GUIHandler.UpdateBoardUI(new List<Move>(), GUIHandler.GenerateLastMoveHighlight());
        }
    }

    /// <summary> Sets up chess game with given FEN code </summary>
    public void SetUpChessBoard(string fenPosition)
    {
        selectedPromotion = byte.MaxValue;
        selectedPiece = byte.MaxValue;
        lastSelectedSquare = byte.MaxValue;

        board = new Board(fenPosition);
        gameState = GameState.Playing;

        GUIHandler.UpdateBotUINull();
        GUIHandler.UpdateBoardUI(new List<Move>(), GUIHandler.ClearColourHighlights());
    }

    /// <summary> Updates bot settings from popup menu. </summary>
    public void AdjustBotDepth(int magnitude)
    {
        Revi.searchDepth = Math.Clamp(Revi.searchDepth + magnitude, 2, 12);
        GUIHandler.UpdateBotSettingsPopup(true);
    }

    /// <summary> Updates bot mode from popup menu. </summary>
    public void AdjustBotMode(int magnitude)
    {
        botMode = (BotMode)Math.Clamp((int)botMode + magnitude, 0, 3);
        GUIHandler.UpdateBotSettingsPopup(true);
    }

    /// <summary> Toggles dynamic bot depth from popup menu. </summary>
    public void ToggleDynamicBotDepth()
    {
        useDynamicDepth = !useDynamicDepth;
        GUIHandler.UpdateBotSettingsPopup(true);
    }


    /// <summary> Updates bot settings to fast bot preset. </summary>
    public void FastBotSettings()
    {
        Revi.searchDepth = 3;
        useDynamicDepth = false;

        GUIHandler.UpdateBotUINull();
    }

    /// <summary> Updates bot settings to smart bot preset. </summary>
    public void SmartBotSettings()
    {
        Revi.searchDepth = 4;
        useDynamicDepth = true;

        GUIHandler.UpdateBotUINull();
    }

    /// <summary> Resets game board. </summary>
    public void ResetGame()
    {
        SetUpChessBoard(Board.usedFen);
    }

    /// <summary> Resets game board. </summary>
    public void UndoMove()
    {
        gameState = GameState.Playing;
        board.UndoMove();
        GUIHandler.UpdateBoardUI(new List<Move>(), GUIHandler.GenerateLastMoveHighlight());
    }
}
