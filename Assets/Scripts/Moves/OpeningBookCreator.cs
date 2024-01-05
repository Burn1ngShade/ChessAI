using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class OpeningBookCreator
{
    public static IEnumerator PGNToOpeningBookFile(string fileName)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        string s = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/{fileName}").text;

        string[] lines = s.Split('\n');

        List<string> moveLines = new List<string>();
        string currentMove = "";
        bool addingMove = false;

        for (int i = 0; i < lines.Length; i++)
        {
            if (i % 300000 == 0)
            {
                UnityEngine.Debug.Log($"Open Book Creator Stage [1 / 2]\nProgress: [{i} / {lines.Length}] {Math.Round((double)i / lines.Length * 100, 2)}%\nElapsed Time: {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)}s");
                yield return null;
            }

            if (lines[i].Length > 0)
            {
                if (lines[i][0] == '1') addingMove = true;
                else if (lines[i][0] == '*')
                {
                    addingMove = false;
                    if (currentMove.Length > 0)
                    {
                        string removedMoveNum = "";
                        for (int j = 0; j < currentMove.Length - 2; j++)
                        {
                            if (currentMove[j + 1] == '.')
                            {
                                j++;
                                continue;
                            }
                            else if (currentMove[j + 2] == '.')
                            {
                                j += 2;
                                continue;
                            }

                            removedMoveNum += currentMove[j];
                        }
                        removedMoveNum += currentMove.Substring(currentMove.Length - 2);
                        removedMoveNum = removedMoveNum.Replace("Q", "");

                        removedMoveNum = Regex.Replace(removedMoveNum, "[^a-zA-Z0-9]", "");
                        moveLines.Add(removedMoveNum);
                    }
                    currentMove = "";
                }
            }

            if (addingMove)
            {
                currentMove += lines[i];
            }
        }

        UnityEngine.Debug.Log($"Open Book Creator Stage [1 / 2]\nProgress: [{lines.Length} / {lines.Length}] 100%\nElapsed Time: {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)}s");
        yield return null;

        lines = moveLines.ToArray();

        Dictionary<ulong, List<string>> openings = new Dictionary<ulong, List<string>>();

        ulong skippedLines = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (i % 2000 == 0)
            {
                UnityEngine.Debug.Log($"Open Book Creator Stage [2 / 2]\nProgress: [{i} / {lines.Length}] {Math.Round((double)i / lines.Length * 100, 2)}%\nElapsed Time: {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)}s, Skipped Lines {skippedLines}");
                yield return null;
            }

            Board board = new Board(Board.defaultFen);

            if (lines[i].Length % 4 != 0)
            {
                skippedLines++;
                continue;
            }

            string[] moves = Enumerable.Range(0, (int)Math.Ceiling((double)lines[i].Length / 4))
            .Select(j => lines[i].Substring(j * 4, 4)).ToArray();

            for (int j = 0; j < moves.Length; j++)
            {

                Move m = board.GetMove(moves[j]);

                if (m == null)
                {
                    skippedLines++;
                    break;
                }

                if (openings.ContainsKey(board.state.zobristKey))
                {
                    if (!openings[board.state.zobristKey].Contains(moves[j])) openings[board.state.zobristKey].Add(moves[j]);
                }
                else
                {
                    openings.Add(board.state.zobristKey, new List<string>() { moves[j] });
                }

                board.MakeMove(m);
            }
        }

        UnityEngine.Debug.Log($"Open Book Creator Stage [2 / 2]\nProgress: [{lines.Length} / {lines.Length}] 100%\nElapsed Time: {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)}s, Skipped Lines {skippedLines}");
        yield return null;

        stopwatch.Stop();
        UnityEngine.Debug.Log($"PGN Converted To Opening Book Format In {stopwatch.ElapsedMilliseconds}ms");

        Revi.openingBook = openings;
    }
}

// [CustomEditor(typeof(OpeningBookCreator))]
// public class OpeningBookCreatorEditor : Editor
// {
//     public override void OnInspectorGUI()
//     {
//         OpeningBookCreator bookCreator = (OpeningBookCreator)target;

//         base.OnInspectorGUI();

//         EditorGUILayout.LabelField("--- Options ---");
//         EditorGUILayout.Space();

//         if (GUILayout.Button("Convert PGN To Opening Book"))
//         {
//             bookCreator.PGNToOpeningBookFile();
//         }
//     }
// }
