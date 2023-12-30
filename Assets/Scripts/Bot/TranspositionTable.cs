using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;

public class TranspositionTable
{
    Dictionary<ulong, (double eval, MoveNode index, double alphaBeta)>[] positions;

    int depth;

    public TranspositionTable(int depth)
    {
        this.depth = depth;

        Clear();
    }

    public void Clear()
    {
        positions = new Dictionary<ulong, (double eval, MoveNode index, double alphaBeta)>[depth + 1];

        for (int i = 0; i <= depth; i++)
        {
            positions[i] = new Dictionary<ulong, (double eval, MoveNode index, double alphaBeta)>();
        }
    }

    public bool Contains(ulong zobristKey, int depth)
    {
        if (depth > this.depth) Debug.Log(depth);
        if (depth < 0) Debug.Log(depth);
        if (positions[depth].ContainsKey(zobristKey)) return true;
        return false;
    }

    public void Add(ulong zobristKey, (double eval, MoveNode index, double alphaBeta) value, int depth)
    {
        positions[depth].Add(zobristKey, value);
    }

    public void Remove(ulong zobristKey, int depth)
    {
        positions[depth].Remove(zobristKey);
    }

    public (double eval, MoveNode index, double alphaBeta) Get(ulong zobristKey, int depth)
    {
        return positions[depth][zobristKey];
    }
}

public class MoveNode
{
    public MoveNode nextNode;

    public int index;
    public int depth;

    public MoveNode(int index, int depth, MoveNode nextNode)
    {
        this.nextNode = nextNode;

        this.index = index;
        this.depth = depth;
    }
}
