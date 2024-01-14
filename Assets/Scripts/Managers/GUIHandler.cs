using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ReviBotPro.SearchDiagnostics;

/// <summary> Class responsible for visualising chess engine </summary>
public class GUIHandler : MonoBehaviour
{
    static GUIHandler Instance;

    static Transform pieceObjHolder;
    static Transform indicatorHolder;

    public static List<Transform> evalUI = new List<Transform>();
    public static List<Transform> infoUI = new List<Transform>();
    public static List<Transform> popupUI = new List<Transform>();

    public static Transform popupBlocker;

    static SpriteRenderer[] boardHighlights = new SpriteRenderer[4];

    public Sprite[] pieceSprites = new Sprite[12];
    public GameObject[] indicators;

    public static int legalMoves = 1;
    public static bool inMenu = false;

    // debug options
    public static bool showAttackBitboard;
    public static bool showPinBitboard;
    public static bool showPossibleAttackBitboard;

    private void Awake()
    {
        pieceObjHolder = transform.GetChild(1);
        indicatorHolder = transform.GetChild(3);

        GameObject canvas = GameObject.Find("Canvas");

        popupBlocker = canvas.transform.GetChild(4);

        foreach (Transform trans in canvas.transform.GetChild(1).transform) evalUI.Add(trans);
        foreach (Transform trans in canvas.transform.GetChild(5).transform) popupUI.Add(trans);
        foreach (Transform trans in canvas.transform.GetChild(2).transform) infoUI.Add(trans);

        for (int i = 0; i <= 3; i++)
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
            Instantiate(GameHandler.board.board[m.endPos] == 0 ? Instance.indicators[0] : Instance.indicators[1], FormattingUtillites.GetWorldPos(m.endPos), Quaternion.identity, indicatorHolder);
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
        infoUI[1].GetChild(0).GetComponent<TMP_Text>().text = $"Turn: {GameHandler.board.turn + 1}";
        infoUI[1].GetChild(1).GetComponent<TMP_Text>().text = $"To Play: {(GameHandler.board.whiteTurn ? "White" : "Black")}";
        infoUI[1].GetChild(2).GetComponent<TMP_Text>().text = $"Legal Move Count: {legalMoves}";

        infoUI[2].GetChild(0).GetComponent<TMP_Text>().text = $"Castle Rights: {BinaryUtilities.GetBinaryRepresentation(GameHandler.board.state.castleRights, 4)}";
        infoUI[2].GetChild(1).GetComponent<TMP_Text>().text = $"50 Move Counter: {GameHandler.board.state.fiftyMoveRule}";
        infoUI[2].GetChild(2).GetComponent<TMP_Text>().text = $"En Passant File: {GameHandler.board.state.enPassantFile}";
        infoUI[2].GetChild(3).GetComponent<TMP_Text>().text = $"In Check: {GameHandler.board.state.isCheck}";

        infoUI[3].GetChild(0).GetComponent<TMP_Text>().text = $"Show Attack Bitboard: {(showAttackBitboard ? "True" : "False")}";
        infoUI[3].GetChild(1).GetComponent<TMP_Text>().text = $"Show Pin Bitboard: {(showPinBitboard ? "True" : "False")}";
        infoUI[3].GetChild(2).GetComponent<TMP_Text>().text = $"Show P Attack Bitboard: {(showPossibleAttackBitboard ? "True" : "False")}";

        UpdatePieceUI();
    }

    /// <summary> Updates UI of bot. </summary>
    public static void UpdateBotUI(Move move, MoveDiagnostics diagnostics)
    {
        UpdateBotSettingsUI();

        if (!move.IsNullMove)
        {
            double eval = diagnostics.eval * (GameHandler.board.whiteTurn ? 1 : -1);
            evalUI[4].GetChild(2).GetComponent<Image>().fillAmount = (Mathf.Clamp((float)eval, -1200, 1200) + 1200) / 2400;
            if (Math.Abs(eval) < 99999) evalUI[4].GetChild(3).GetComponent<TMP_Text>().text = $"{(eval > 0 ? "+" : "")}{((double)eval / 100).ToString("0.00")}";
            else evalUI[4].GetChild(3).GetComponent<TMP_Text>().text = "Forced Checkmate";

            evalUI[2].GetChild(0).GetComponent<TMP_Text>().text = $"Type: {(diagnostics.moveType == 0 ? "Full Search" : diagnostics.moveType == 1 ? "Book Move" : "Transposition")}";
            evalUI[2].GetChild(1).GetComponent<TMP_Text>().text = $"Used Depth: {(diagnostics.moveType != 0 ? "None" : diagnostics.depth)}";
            evalUI[2].GetChild(2).GetComponent<TMP_Text>().text = $"Time Taken: {Math.Round(diagnostics.timeTaken, 2)}s";
            evalUI[2].GetChild(3).GetComponent<TMP_Text>().text = $"Time Per Move: {(diagnostics.movesSearched == 0 ? 0 : Math.Round(diagnostics.timeTaken * 1000 / Math.Max(diagnostics.movesSearched, 1), 3))}ms";

            evalUI[3].GetChild(0).GetComponent<TMP_Text>().text = $"Moves Searched: {diagnostics.movesSearched}";
            evalUI[3].GetChild(1).GetComponent<TMP_Text>().text = $"Best Move: {FormattingUtillites.BoardCode(move.startPos)} -> {FormattingUtillites.BoardCode(move.endPos)}";
            evalUI[3].GetChild(2).GetComponent<TMP_Text>().text = $"Best Move Eval: {(diagnostics.moveType != 0 ? "None" :  Math.Round(eval, 2))}";
            evalUI[3].GetChild(3).GetComponent<TMP_Text>().text = $"Transpositions: {diagnostics.transpositions}";
        }
    }

    /// <summary> Reset bot UI to starting state. </summary>
    public static void ResetBotUI()
    {
        UpdateBotSettingsUI();

        evalUI[4].GetChild(2).GetComponent<Image>().fillAmount = 0.5f;
        evalUI[4].GetChild(3).GetComponent<TMP_Text>().text = "0.00";

        evalUI[2].GetChild(0).GetComponent<TMP_Text>().text = $"Type: None";
        evalUI[2].GetChild(1).GetComponent<TMP_Text>().text = $"Used Depth: None";
        evalUI[2].GetChild(2).GetComponent<TMP_Text>().text = $"Time Taken: 0s";
        evalUI[2].GetChild(3).GetComponent<TMP_Text>().text = $"Time Per Move: 0ms";

        evalUI[3].GetChild(0).GetComponent<TMP_Text>().text = $"Moves Searched: 0";
        evalUI[3].GetChild(1).GetComponent<TMP_Text>().text = $"Best Move: None";
        evalUI[3].GetChild(2).GetComponent<TMP_Text>().text = $"Best Move Eval: None";
        evalUI[3].GetChild(3).GetComponent<TMP_Text>().text = $"Transpositions: 0";
    }

    /// <summary> Updates UI of bot settings. </summary>
    public static void UpdateBotSettingsUI()
    {
        evalUI[1].GetChild(0).GetComponent<TMP_Text>().text = $"Bot Mode: {GameHandler.botMode}";
        evalUI[1].GetChild(1).GetComponent<TMP_Text>().text = $"Opening Book Mode: {(GameHandler.openBookMode == -2 ? "Off" : GameHandler.openBookMode == -1 ? "Best" : $"{(double)GameHandler.openBookMode / 4}")}";
        evalUI[1].GetChild(2).GetComponent<TMP_Text>().text = $"Iterative Deepening: {GameHandler.iterativeDeepening}";
        evalUI[1].GetChild(3).GetComponent<TMP_Text>().text = $"Inital Depth: {GameHandler.botSearchDepth}";
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
                g.transform.position = FormattingUtillites.GetWorldPos((byte)i);
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
        if (showPossibleAttackBitboard)
        {
            Color[] colours = new Color[64];
            for (int i = 0; i < 64; i++)
            {
                if ((GameHandler.board.wPossbileAttackBitboard & GameHandler.board.bPossbileAttackBitboard & (1UL << i)) != 0) colours[i] = new Color(0, 1, 0, 1);
                else if ((GameHandler.board.wPossbileAttackBitboard & (1UL << i)) != 0) colours[i] = new Color(1, 0, 0, 1);
                else if ((GameHandler.board.bPossbileAttackBitboard & (1UL << i)) != 0) colours[i] = new Color(0, 0, 1, 1);
                else colours[i] = new Color(0, 0, 0, 0);
            }
            UpdateBoardHighlights(3, colours);
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
    "", "White Checkmate!", "Black Checkmate!", "Draw Via Stalemate!", "Draw Via 50 Move Rule!", "Draw Via Repetition!"};

    /// <summary> Toggles end game ui. </summary>
    public static void ToggleEndGameUI(int state)
    {
        inMenu = state == 0 ? true : false;
        popupBlocker.gameObject.SetActive(state == 0 ? true : false);
        popupUI[1].gameObject.SetActive(state == 0 ? true : false);
        popupUI[1].GetChild(1).GetComponent<TMP_Text>().text = endGameMessages[GameHandler.board.state.gameState];
        if (state == 1) GameHandler.SetUpChessBoard(Board.usedFen);
    }

    /// <summary> Toggles promotion ui, non 0 state updates selected promotion. </summary>
    public static void TogglePromotionUI(int state)
    {
        popupBlocker.gameObject.SetActive(state == 0 ? true : false);
        popupUI[0].gameObject.SetActive(state == 0 ? true : false);
        inMenu = state == 0 ? true : false;

        if (state != 0) GameHandler.selectedPromotion = (byte)state;
        else
        {
            for (int i = 0; i < 4; i++)
            {
                popupUI[0].GetChild(2).GetChild(i).GetChild(1).GetComponent<Image>().sprite = Instance.pieceSprites[i + (GameHandler.board.whiteTurn ? 1 : 7)];
            }
        }
    }

    /// <summary> updates bot popup ui. </summary>
    public static void ToggleFenSettingsPopup(bool enabled)
    {
        popupBlocker.gameObject.SetActive(enabled);
        inMenu = enabled;

        popupUI[3].gameObject.SetActive(enabled);
        popupUI[3].GetChild(2).GetChild(1).GetComponent<TMP_Text>().text = FormattingUtillites.BoardToFenString(GameHandler.board);
    }

    /// <summary> Copy current game fen to clipboard. </summary>
    public static void CopyFenToClipboard()
    {
        GUIUtility.systemCopyBuffer = FormattingUtillites.BoardToFenString(GameHandler.board);
    }

    /// <summary> Copy current game pgn to clipboard. </summary>
    public static void CopyPgnToClipboard()
    {
        GUIUtility.systemCopyBuffer = FormattingUtillites.BoardToPgnString(GameHandler.board);
    }

    /// <summary> Load given fen position (in game settings). </summary>
    public static void LoadFenPosition()
    {
        string loadedFen = popupUI[3].GetChild(5).GetComponent<TMP_InputField>().text;
        if (FormattingUtillites.FenValid(loadedFen)) GameHandler.SetUpChessBoard(loadedFen);
    }

    /// <summary> Updates bot popup ui. </summary>
    public static void UpdateBotSettingsPopup(bool enabled)
    {
        Instance.StartCoroutine(IUpdateBotSettingsPopup(enabled));
    }

    /// <summary> Updates bot popup ui (IEnumator gives time for popup to go). </summary>
    static IEnumerator IUpdateBotSettingsPopup(bool enabled)
    {
        bool oldActive = popupUI[2].gameObject.activeSelf;

        popupBlocker.gameObject.SetActive(enabled);
        popupUI[2].gameObject.SetActive(enabled);

        popupUI[2].GetChild(5).GetChild(1).GetComponent<TMP_Text>().text = $"{(GameHandler.openBookMode == -2 ? "Off" : GameHandler.openBookMode == -1 ? "Best" : $"{(double)GameHandler.openBookMode / 4}")}";
        popupUI[2].GetChild(4).GetChild(1).GetComponent<TMP_Text>().text = $"{GameHandler.botMode}";
        popupUI[2].GetChild(3).GetChild(1).GetComponent<TMP_Text>().text = $"{GameHandler.iterativeDeepening}";
        popupUI[2].GetChild(2).GetChild(1).GetComponent<TMP_Text>().text = $"{GameHandler.botSearchDepth}";

        UpdateBotUI(Move.NullMove, new MoveDiagnostics());

        yield return null;

        if (enabled != oldActive)
        {
            if (!enabled) inMenu = false;
            else inMenu = true;
        }
    }
}
