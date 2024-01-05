using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Responsible for handling user and ai interaction with chess game </summary>
public class GameHandler : MonoBehaviour
{
    public static GameHandler Instance;

    public static Board board;

    enum GameState { Playing, Promotion, Over }
    GameState gameState = GameState.Playing;

    byte selectedPiece = byte.MaxValue;
    byte lastSelectedSquare = byte.MaxValue;
    public static byte selectedPromotion = byte.MaxValue;

    public static int botMode = 0;
    public static bool useDynamicDepth = true;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(OpeningBookCreator.PGNToOpeningBookFile("Perfect2023OpeningBook.txt"));
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
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            botMode = 0;
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            botMode = 1;
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            botMode = 2;
            GUIHandler.UpdateBotUINull();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            botMode = 3;
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
        switch (gameState)
        {
            case GameState.Playing:
                if (board.state.gameState != 0)
                {
                    gameState = GameState.Over;
                    GUIHandler.ToggleEndGameUI(0);
                    return;
                }

                if (board.whiteTurn && (botMode == 1 || botMode == 3)) HandleBotGameplay();
                else if (!board.whiteTurn && (botMode == 2 || botMode == 3)) HandleBotGameplay();
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
                        Debug.Log(Evaluation.Evaluate(board));
                    }


                    GUIHandler.UpdateBoardUI(new List<Move>(), GUIHandler.GenerateLastMoveHighlight());

                    selectedPiece = byte.MaxValue;
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.U)) //undo move
        {
            board.UndoMove();
            GUIHandler.UpdateBoardUI(new List<Move>(), GUIHandler.GenerateLastMoveHighlight());
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
            Debug.Log(Evaluation.Evaluate(board));

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
    public void EditBotDepth(int editBy)
    {
        Revi.searchDepth = Math.Clamp(Revi.searchDepth + editBy, 2, 12);
        GUIHandler.UpdateBotSettingsPopup(true);
    }

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
}
