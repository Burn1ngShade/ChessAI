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
    public static Dictionary<ulong, List<string>> PGNToOpeningBookFile(string fileName)
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

        lines = moveLines.ToArray();

        Dictionary<ulong, List<string>> openings = new Dictionary<ulong, List<string>>();

        for (int i = 0; i < lines.Length; i++)
        {
            Board board = new Board(Board.defaultFen);

            string[] moves = Enumerable.Range(0, (int)Math.Ceiling((double)lines[i].Length / 4))
            .Select(j => lines[i].Substring(j * 4, 4)).ToArray();

            for (int j = 0; j < moves.Length; j++)
            {
                if (openings.ContainsKey(board.zobristKey))
                {
                    if (!openings[board.zobristKey].Contains(moves[j])) openings[board.zobristKey].Add(moves[j]);
                }
                else
                {
                    openings.Add(board.zobristKey, new List<string>() { moves[j] });
                }

                board.MakeMove(board.GetMove(moves[j]));
            }
        }

        stopwatch.Stop();
        UnityEngine.Debug.Log($"PGN Converted To Opening Book Format In {stopwatch.ElapsedMilliseconds}ms");

        return openings;
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
