using System.Collections.Generic;
using UnityEngine;

/// <summary> Class storing chess positions, and their data </summary>
public class TranspositionTablePro
{
    /// zobrist / eval
    Dictionary<ulong, double>[] positions;

    int depth;

    public TranspositionTablePro(int depth)
    {
        this.depth = depth;

        Clear();
    }

    /// <summary> Clear all data from transposition table. </summary>
    public void Clear()
    {
        positions = new Dictionary<ulong, double>[depth];

        for (int i = 0; i < depth; i++)
        {
            positions[i] = new Dictionary<ulong, double>();
        }
    }

    /// <summary> Checks if table contains given move, at given depth. </summary>
    public bool Contains(ulong zobristKey, int depth)
    {
        if (positions[depth].ContainsKey(zobristKey)) return true;
        return false;
    }

    /// <summary> Add given move data, at given depth. </summary>
    public void Add(ulong zobristKey, double value, int depth)
    {
        positions[depth].Add(zobristKey, value);
    }

    /// <summary> Remove given move data, at given depth. </summary>
    public void Remove(ulong zobristKey, int depth)
    {
        positions[depth].Remove(zobristKey);
    }

    /// <summary> Get move at given position and depth. </summary>
    public double Get(ulong zobristKey, int depth)
    {
        return positions[depth][zobristKey];
    }
}
