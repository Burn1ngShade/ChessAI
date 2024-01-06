using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BinaryExtras
{
    /// <summary> Converts given ulong to binary format (e.g 5 -> 101). </summary>
    public static string GetBinaryRepresentation(ulong value, int formatLength = 64)
    {
        string binary = Convert.ToString((long)value, 2);
        string padding = new string('0', Math.Max(formatLength - binary.Length, 0));
        return $"0b{padding}{binary}";
    }

    public static double PopCount(ulong value)
    {
        int count = 0;
        while (value != 0)
        {
            count++;
            value &= value - 1;
        }
        return count;
    }

    public static byte PopCount(byte value)
    {
        byte count = 0;
        while (value != 0)
        {
            count++;
            value &= (byte)(value- 1);
        }
        return count;
    }

    public static int FlipBitboardIndex(int index)
    {
        return (index)^56;
    }

    public static bool BitboardContains(ulong bitboard, int index)
    {
        return (bitboard & (1UL << index)) != 0;
    }

    public static bool ByteContains(byte b, int index)
    {
        return (b & (1 << index)) != 0;
    }
}
