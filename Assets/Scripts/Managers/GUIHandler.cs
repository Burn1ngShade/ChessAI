using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary> Class responsible for visualising chess engine </summary>
public class GUIHandler : MonoBehaviour
{
    static GUIHandler Instance;

    static Transform pieceObjHolder;
    static Transform indicatorHolder;

    public static List<Transform> evalUI = new List<Transform>();
    public static List<Transform> infoUI = new List<Transform>();
    public static List<Transform> popupUI = new List<Transform>();

    static SpriteRenderer[] boardHighlights = new SpriteRenderer[3];

    public Sprite[] pieceSprites = new Sprite[12];
    public GameObject[] indicators;

    public static int legalMoves = 1;

    // debug options
    public static bool showAttackBitboard;
    public static bool showPinBitboard;

    private void Awake()
    {
        pieceObjHolder = transform.GetChild(1);
        indicatorHolder = transform.GetChild(3);

        GameObject canvas = GameObject.Find("Canvas");

        foreach (Transform trans in canvas.transform.GetChild(0).transform) evalUI.Add(trans);
        foreach (Transform trans in canvas.transform.GetChild(1).transform) popupUI.Add(trans);
        foreach (Transform trans in canvas.transform.GetChild(2).transform) infoUI.Add(trans);

        for (int i = 0; i <= 2; i++)
        {
            boardHighlights[i] = transform.GetChild(2).GetChild(i).GetComponent<SpriteRenderer>();
            UpdateBoardHighlights(i, ClearColourHighlights());
        }

        Instance = this;
    }

    // --- UI UPDATES ---

    /// <summary> Updates move indicator UI elements. </summary>
    public static void UpdateIndicatorUI(List<Move> moves)
    {
        foreach (Transform child in indicatorHolder)
        {
            Destroy(child.gameObject);
        }

        foreach (Move m in moves)
        {
            Instantiate(GameHandler.board.board[m.endPos] == 0 ? Instance.indicators[0] : Instance.indicators[1], Board.GetWorldPos(m.endPos), Quaternion.identity, indicatorHolder);
        }
    }

    /// <summary> Board UI update. </summary>
    public static void UpdateBoardUI(List<Move> moves, Color[] colours)
    {
        UpdateUI();
        UpdateIndicatorUI(moves);
        UpdateBoardHighlights(0, colours);
    }

    /// <summary> Updates constant UI elements. </summary>
    public static void UpdateUI()
    {
        infoUI[0].GetComponent<TMP_Text>().text = $"Turn: {GameHandler.board.turn + 1}";
        infoUI[1].GetComponent<TMP_Text>().text = $"To Play: {(GameHandler.board.whiteTurn ? "White" : "Black")}";
        infoUI[2].GetComponent<TMP_Text>().text = $"Move Count: {legalMoves}";
        infoUI[3].GetComponent<TMP_Text>().text = $"Castle Rights: {BinaryExtras.GetBinaryRepresentation(GameHandler.board.state.castleRights, 4)}";
        infoUI[4].GetComponent<TMP_Text>().text = $"50 Move Counter: {GameHandler.board.state.fiftyMoveRule}";
        infoUI[5].GetComponent<TMP_Text>().text = $"En Passant File: {GameHandler.board.state.enPassantFile}";
        infoUI[6].GetComponent<TMP_Text>().text = $"Show Attack Bitboard: {(showAttackBitboard ? "True" : "False")}";
        infoUI[7].GetComponent<TMP_Text>().text = $"Show Pin Bitboard: {(showPinBitboard ? "True" : "False")}";

        UpdatePieceUI();
    }

    /// <summary> Updates UI elements about BOT. </summary>
    public static void UpdateBotUI(Move move, double eval, int movesSearched, int searchDepth, int transpositions, int transpositionsUsed, TimeSpan timeTaken)
    {
        evalUI[13].GetChild(2).GetComponent<Image>().fillAmount = (Mathf.Clamp((float)eval, -1200, 1200) + 1200) / 2400;
        evalUI[13].GetChild(3).GetComponent<TMP_Text>().text = $"{(eval >= 0 ? "+" : "")}{((double)eval / 100).ToString("0.00")}";
        evalUI[1].GetComponent<TMP_Text>().text = $"Bot Mode: {(GameHandler.botMode == 0 ? "Off" : GameHandler.botMode == 1 ? "White" : GameHandler.botMode == 2 ? "Black" : "Both")}";
        evalUI[2].GetComponent<TMP_Text>().text = $"Default Bot Depth: {Revi.searchDepth} ({Revi.searchDepth + 1})";
        evalUI[3].GetComponent<TMP_Text>().text = $"Used Bot Depth: {searchDepth} ({searchDepth + 1})";
        evalUI[4].GetComponent<TMP_Text>().text = $"Bot Capture Depth: {searchDepth + (searchDepth % 2 == 0 ? 2 : 1)} ({searchDepth + ((searchDepth % 2 == 0 ? 3 : 2))})";
        evalUI[5].GetComponent<TMP_Text>().text = $"Dynamic Bot Depth: {GameHandler.useDynamicDepth}"; //cant disable yet lol
        evalUI[6].GetComponent<TMP_Text>().text = $"Moves Searched: {movesSearched}";
        evalUI[7].GetComponent<TMP_Text>().text = $"Transpos Found: {transpositions}";
        evalUI[8].GetComponent<TMP_Text>().text = $"Transpos Used: {transpositionsUsed}";
        evalUI[9].GetComponent<TMP_Text>().text = $"Best Move: {Piece.AlgebraicNotation(move.startPos)} -> {Piece.AlgebraicNotation(move.endPos)}";
        evalUI[10].GetComponent<TMP_Text>().text = $"Best Move Eval: {Math.Round(eval, 2)}";
        evalUI[11].GetComponent<TMP_Text>().text = $"Time Taken: {Math.Round(timeTaken.TotalSeconds, 2)}s";
        evalUI[12].GetComponent<TMP_Text>().text = $"Time Per Move: {Math.Round(timeTaken.TotalMilliseconds / (movesSearched + 1), 3)}ms";
    }

    /// <summary> Updates UI elements about BOT To Unknown Values. </summary>
    public static void UpdateBotUINull()
    {
        evalUI[13].GetChild(2).GetComponent<Image>().fillAmount = 0.5f;
        evalUI[13].GetChild(3).GetComponent<TMP_Text>().text = "???";
        evalUI[1].GetComponent<TMP_Text>().text = $"Bot Mode: {(GameHandler.botMode == 0 ? "Off" : GameHandler.botMode == 1 ? "White" : GameHandler.botMode == 2 ? "Black" : "Both")}";
        evalUI[2].GetComponent<TMP_Text>().text = $"Default Bot Depth: {Revi.searchDepth} ({Revi.searchDepth + 1})";
        evalUI[3].GetComponent<TMP_Text>().text = $"Used Bot Depth: {Revi.searchDepth} ({Revi.searchDepth + 1})";
        evalUI[4].GetComponent<TMP_Text>().text = $"Bot Capture Depth: {Revi.searchDepth + (Revi.searchDepth % 2 == 0 ? 2 : 1)} ({Revi.searchDepth + ((Revi.searchDepth % 2 == 0 ? 3 : 2))})";
        evalUI[5].GetComponent<TMP_Text>().text = $"Dynamic Bot Depth: {GameHandler.useDynamicDepth}"; //cant disable yet lol
        evalUI[6].GetComponent<TMP_Text>().text = $"Moves Searched: 0";
        evalUI[7].GetComponent<TMP_Text>().text = $"Transpos Found: 0";
        evalUI[8].GetComponent<TMP_Text>().text = $"Transpos Used: 0";
        evalUI[9].GetComponent<TMP_Text>().text = $"Best Move: None";
        evalUI[10].GetComponent<TMP_Text>().text = $"Best Move Eval: ???";
        evalUI[11].GetComponent<TMP_Text>().text = $"Time Taken: 0s";
        evalUI[12].GetComponent<TMP_Text>().text = $"Time Per Move: 0ms";
    }

    /// <summary> Updates piece UI elements. </summary>
    public static void UpdatePieceUI()
    {
        foreach (Transform child in pieceObjHolder)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i <= 63; i++)
        {
            if (((GameHandler.board.wPieceBitboard & (1UL << i)) != 0) || ((GameHandler.board.bPieceBitboard & (1UL << i)) != 0))
            {
                GameObject g = new GameObject($"Piece {i}");
                g.AddComponent<SpriteRenderer>().sprite = Instance.pieceSprites[GameHandler.board.board[i] - 1];
                g.GetComponent<SpriteRenderer>().sortingOrder = 99;
                g.transform.SetParent(pieceObjHolder);
                g.transform.position = Board.GetWorldPos((byte)i);
                g.transform.localScale = new Vector3(0.8f, 0.8f, 1);
            }

        }
    }

    // --- BOARD HIGHLIGHTS ---

    static Color highlightColour = new Color(0.73f, 0.8f, 0.27f, 1);

    /// <summary> Generates color[] of last move, and give index. </summary>
    public static Color[] GenerateLastMoveHighlight(int index = -1)
    {
        Color[] highlight = Enumerable.Repeat(new Color(0f, 0, 0f, 0f), 64).ToArray();
        if (index != -1) highlight[index] = highlightColour;

        if (GameHandler.board.previousMoves.Count > 0)
        {
            Move m = GameHandler.board.previousMoves.Peek();
            highlight[m.startPos] = highlightColour;
            highlight[m.endPos] = highlightColour;
        }

        return highlight;
    }

    /// <summary> Updates board debug highlights. </summary>
    public static void UpdateBoardHighlights()
    {
        if (showAttackBitboard)
        {
            Color[] colours = new Color[64];
            for (int i = 0; i <= 63; i++)
            {
                if ((GameHandler.board.wAttackBitboard & GameHandler.board.bAttackBitboard & (1UL << i)) != 0) colours[i] = new Color(0, 1, 0, 1);
                else if ((GameHandler.board.wAttackBitboard & (1UL << i)) != 0) colours[i] = new Color(1, 0, 0, 1);
                else if ((GameHandler.board.bAttackBitboard & (1UL << i)) != 0) colours[i] = new Color(0, 0, 1, 1);
                else colours[i] = new Color(0, 0, 0, 0);
            }
            UpdateBoardHighlights(1, colours);
        }
        if (showPinBitboard)
        {
            Color[] colours = new Color[64];
            for (int i = 0; i < 64; i++)
            {
                if ((GameHandler.board.pinBitboard & (1UL << i)) != 0) colours[i] = new Color(1, 0, 0, 1);
                else if ((GameHandler.board.checkBitboard & (1UL << i)) != 0) colours[i] = new Color(0, 1, 0, 1);
                else if ((GameHandler.board.kingBlockerBitboard & (1UL << i)) != 0) colours[i] = new Color(0, 0, 1, 1);
            }
            UpdateBoardHighlights(2, colours);
        }
    }

    /// <summary> Updates given board, with new colour[64]. </summary>
    public static void UpdateBoardHighlights(int index, Color[] colours)
    {
        if (colours.Length != 64) return;

        Texture2D texture2D = new Texture2D(8, 8);
        texture2D.SetPixels(colours);
        texture2D.filterMode = FilterMode.Point;
        texture2D.Apply();

        boardHighlights[index].sprite = Sprite.Create(texture2D, new Rect(0, 0, 8, 8), Vector2.zero, 1);
    }

    /// <summary> new clear colour[64]. </summary>
    public static Color[] ClearColourHighlights()
    {
        return Enumerable.Repeat(new Color(0f, 0f, 0f, 0f), 64).ToArray();
    }

    // --- POP UPS ---

    static string[] endGameMessages = new string[] {
    "", "White Checkmate!", "Black Checkmate!", "Stalemate!", "Draw Via 50 Move Rule!", "Draw Via Repetition!"};

    /// <summary> Toggles end game ui. </summary>
    public static void ToggleEndGameUI(int state)
    {
        popupUI[1].gameObject.SetActive(state == 0 ? true : false);
        popupUI[1].GetChild(2).GetComponent<TMP_Text>().text = endGameMessages[GameHandler.board.state.gameState];
        if (state == 1) GameHandler.Instance.SetUpChessBoard(Board.usedFen);
    }

    /// <summary> Toggles promotion ui, non 0 state updates selected promotion. </summary>
    public static void TogglePromotionUI(int state)
    {
        popupUI[0].gameObject.SetActive(state == 0 ? true : false);

        if (state != 0) GameHandler.selectedPromotion = (byte)state;
        else
        {
            for (int i = 5; i <= 8; i++)
            {
                popupUI[0].GetChild(3).GetChild(i - 1).GetComponent<Image>().sprite = Instance.pieceSprites[i - 4 + (GameHandler.board.whiteTurn ? 0 : 6)];
            }
        }
    }

    /// <summary> updates bot popup ui. </summary>
    public static void UpdateBotSettingsPopup(bool enabled)
    {
        popupUI[2].gameObject.SetActive(enabled);

        popupUI[2].GetChild(3).GetComponent<TMP_Text>().text = $"Bot Depth: {Revi.searchDepth}";
        popupUI[2].GetChild(4).GetComponent<TMP_Text>().text = $"Use Dynamic Depth: {GameHandler.useDynamicDepth}";
        GUIHandler.UpdateBotUINull();
    }
}
