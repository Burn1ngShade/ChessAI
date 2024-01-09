using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary> Responsible for handling user and ai interaction with chess game </summary>
public class GameHandler : MonoBehaviour
{
    public static GameHandler Instance;

    public static Board board;

    public enum GameState { Playing, Promotion, Over }
    public static GameState gameState = GameState.Playing;

    static byte selectedPiece = byte.MaxValue;
    static byte lastSelectedSquare = byte.MaxValue;
    public static byte selectedPromotion = byte.MaxValue;

    public readonly List<(KeyCode hotkey, Action action)> hotkeys = new List<(KeyCode hotkey, Action action)>() {
        (KeyCode.R, new Action(() => SetUpChessBoard(Board.usedFen))),
        (KeyCode.U, new Action(() => UndoMove())),
        (KeyCode.Alpha1, new Action(() => { ReviBot.botMode = (ReviBot.BotMode)0; GUIHandler.UpdateBotUINull(); })),
        (KeyCode.Alpha2, new Action(() => { ReviBot.botMode = (ReviBot.BotMode)1; GUIHandler.UpdateBotUINull(); })),
        (KeyCode.Alpha3, new Action(() => { ReviBot.botMode = (ReviBot.BotMode)2; GUIHandler.UpdateBotUINull(); })),
        (KeyCode.Alpha4, new Action(() => { ReviBot.botMode = (ReviBot.BotMode)3; GUIHandler.UpdateBotUINull(); })),
        (KeyCode.F, new Action(() => { GUIHandler.showAttackBitboard = !GUIHandler.showAttackBitboard; GUIHandler.UpdateUI(); if (GUIHandler.showAttackBitboard)   GUIHandler.UpdateBoardHighlights(1, GUIHandler.ClearColourHighlights());} )),
        (KeyCode.G, new Action(() => { GUIHandler.showPinBitboard = !GUIHandler.showPinBitboard; GUIHandler.UpdateUI(); if (GUIHandler.showPinBitboard)   GUIHandler.UpdateBoardHighlights(2, GUIHandler.ClearColourHighlights());} )),
        (KeyCode.H, new Action(() => { GUIHandler.showPossibleAttackBitboard = !GUIHandler.showPossibleAttackBitboard; GUIHandler.UpdateUI(); if (GUIHandler.showPossibleAttackBitboard)   GUIHandler.UpdateBoardHighlights(3, GUIHandler.ClearColourHighlights());} )),
        (KeyCode.J, new Action(() => {ReviBot.searchDepth = Math.Clamp(ReviBot.searchDepth - 1, 2, 8); GUIHandler.UpdateBotUINull();})),
        (KeyCode.K, new Action(() => {ReviBot.searchDepth = Math.Clamp(ReviBot.searchDepth + 1, 2, 8); GUIHandler.UpdateBotUINull();})),
        (KeyCode.L, new Action(() => {ReviBot.useDynamicDepth = !ReviBot.useDynamicDepth; GUIHandler.UpdateBotUINull();})),
    };

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ReviBot.openingBook = new OpeningBook(File.ReadAllText("Assets/Book.txt"));
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
        if (GUIHandler.inMenu) return;

        foreach ((KeyCode hotkey, Action action) in hotkeys){
            if (Input.GetKeyDown(hotkey)){
                action.Invoke();
            }
        }

        GUIHandler.UpdateBoardHighlights();
    }

    /// <summary> Updates game interaction of the chess engine. </summary>
    void UpdateGameInteraction()
    {
        if (GUIHandler.inMenu) return;

        switch (gameState)
        {
            case GameState.Playing:
                if (board.state.gameState != 0)
                {
                    gameState = GameState.Over;
                    GUIHandler.ToggleEndGameUI(0);
                    return;
                }

                if (board.whiteTurn && (ReviBot.botMode == ReviBot.BotMode.White || ReviBot.botMode == ReviBot.BotMode.Both)) HandleBotGameplay();
                else if (!board.whiteTurn && (ReviBot.botMode == ReviBot.BotMode.Black || ReviBot.botMode == ReviBot.BotMode.Both)) HandleBotGameplay();
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
        Move m = ReviBot.GetMove(board, ReviBot.searchDepth, ReviBot.useDynamicDepth, ReviBot.openingBookMode);

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
                byte index = FormattingUtillites.GetIndexPos(mousePosition);
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
    public static void SetUpChessBoard(string fenPosition)
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
        ReviBot.searchDepth = Math.Clamp(ReviBot.searchDepth + magnitude, 2, 12);
        GUIHandler.UpdateBotSettingsPopup(true);
    }

    /// <summary> Updates bot mode from popup menu. </summary>
    public void AdjustBotMode(int magnitude)
    {
        ReviBot.botMode = (ReviBot.BotMode)Math.Clamp((int)ReviBot.botMode + magnitude, 0, 3);
        GUIHandler.UpdateBotSettingsPopup(true);
    }

    public void AdjustOpeningBookMode(int magnitude)
    {
        ReviBot.openingBookMode = ReviBot.openingBookMode + magnitude < -2 ? 4 : ReviBot.openingBookMode + magnitude > 4 ? -2 : ReviBot.openingBookMode + magnitude;
        GUIHandler.UpdateBotSettingsPopup(true);
    }

    /// <summary> Toggles dynamic bot depth from popup menu. </summary>
    public void ToggleDynamicBotDepth()
    {
        ReviBot.useDynamicDepth = !ReviBot.useDynamicDepth;
        GUIHandler.UpdateBotSettingsPopup(true);
    }


    /// <summary> Updates bot settings to fast bot preset. </summary>
    public void FastBotSettings()
    {
        ReviBot.searchDepth = 3;
        ReviBot.useDynamicDepth = false;

        GUIHandler.UpdateBotUINull();
    }

    /// <summary> Updates bot settings to smart bot preset. </summary>
    public void SmartBotSettings()
    {
        ReviBot.searchDepth = 4;
        ReviBot.useDynamicDepth = true;

        GUIHandler.UpdateBotUINull();
    }

    /// <summary> Resets game board. </summary>
    public void ResetGame()
    {
        SetUpChessBoard(Board.usedFen);
    }

    /// <summary> Resets game board. </summary>
    public static void UndoMove()
    {
        gameState = GameState.Playing;
        board.UndoMove();
        GUIHandler.UpdateBoardUI(new List<Move>(), GUIHandler.GenerateLastMoveHighlight());
    }
}
