using System;

/// <summary> Useful functions for working with binary. </summary>
public static class BinaryUtilities
{
    /// <summary> Converts given ulong to binary format (e.g 5 -> 101). </summary>
    public static string GetBinaryRepresentation(ulong value, int formatLength = 64)
    {
        string binary = Convert.ToString((long)value, 2);
        string padding = new string('0', Math.Max(formatLength - binary.Length, 0));
        return $"0b{padding}{binary}";
    }

    /// <summary> Gets population count (number of 1s) in given byte. </summary>
    public static byte PopCount(byte value)
    {
        byte count = 0;
        while (value != 0)
        {
            count++;
            value &= (byte)(value - 1);
        }
        return count;
    }

    /// <summary> Flips board index, e.g a8 -> a1. </summary>
    public static int FlipBitboardIndex(int index)
    {
        return (index) ^ 56;
    }

    /// <summary> Returns value of bit of bitboard, at given index. </summary>
    public static bool BitboardContains(ulong bitboard, int index)
    {
        return (bitboard & (1UL << index)) != 0;
    }

    /// <summary> Returns value of bit of byte, at given index. </summary>
    public static bool ByteContains(byte b, int index)
    {
        return (b & (1 << index)) != 0;
    }
}
