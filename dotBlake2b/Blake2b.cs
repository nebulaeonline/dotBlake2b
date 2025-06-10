using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace nebulae.dotBlake2b;

public static class Blake2b
{
    // Set to true to force scalar implementation, useful for testing 
    // or benchmarking against AVX2 implementation.
    public static bool ForceScalar = false;

    internal static readonly ulong[] IV =
    [
        0x6a09e667f3bcc908ul, 0xbb67ae8584caa73bul,
        0x3c6ef372fe94f82bul, 0xa54ff53a5f1d36f1ul,
        0x510e527fade682d1ul, 0x9b05688c2b3e6c1ful,
        0x1f83d9abfb41bd6bul, 0x5be0cd19137e2179ul
    ];

    private static readonly byte[,] Sigma =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9,10,11,12,13,14,15 },
        {14,10, 4, 8, 9,15,13, 6, 1,12, 0, 2,11, 7, 5, 3 },
        {11, 8,12, 0, 5, 2,15,13,10,14, 3, 6, 7, 1, 9, 4 },
        { 7, 9, 3, 1,13,12,11,14, 2, 6, 5,10, 4, 0,15, 8 },
        { 9, 0, 5, 7, 2, 4,10,15,14, 1,11,12, 6, 8, 3,13 },
        { 2,12, 6,10, 0,11, 8, 3, 4,13, 7, 5,15,14, 1, 9 },
        {12, 5, 1,15,14,13, 4,10, 0, 7, 6, 3, 9, 2, 8,11 },
        {13,11, 7,14,12, 1, 3, 9, 5, 0,15, 4, 8, 6, 2,10 },
        { 6,15,14, 9,11, 3, 0, 8,12, 2,13, 7, 1, 4,10, 5 },
        {10, 2, 8, 4, 7, 6, 1, 5,15,11, 9,14, 3,12,13, 0 },
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9,10,11,12,13,14,15 },
        {14,10, 4, 8, 9,15,13, 6, 1,12, 0, 2,11, 7, 5, 3 }
    };

    public static void ComputeHash(ReadOnlySpan<byte> input, Span<byte> output, int outputLength)
    {
        if (output.Length < outputLength)
            throw new ArgumentException("Output buffer too small", nameof(output));

        Span<byte> paramBlock = stackalloc byte[64];
        paramBlock[0] = (byte)outputLength;     // digest_length
        paramBlock[1] = 0;                      // key_length
        paramBlock[2] = 1;                      // fanout
        paramBlock[3] = 1;                      // depth
        BinaryPrimitives.WriteUInt32LittleEndian(paramBlock.Slice(4), 0);  // leaf_length
        BinaryPrimitives.WriteUInt64LittleEndian(paramBlock.Slice(8), 0);  // node_offset
        paramBlock[16] = 0;                     // node_depth
        paramBlock[17] = 0;                     // inner_length
        paramBlock.Slice(18, 14).Clear();      // reserved
        paramBlock.Slice(32, 16).Clear();      // salt
        paramBlock.Slice(48, 16).Clear();      // personal

        // Initialize h[0..7] = IV[i] ^ paramBlock[i]
        Span<ulong> h = stackalloc ulong[8];
        for (int i = 0; i < 8; i++)
        {
            ulong paramWord = BinaryPrimitives.ReadUInt64LittleEndian(paramBlock.Slice(i * 8, 8));
            h[i] = IV[i] ^ paramWord;
        }

        Span<byte> block = stackalloc byte[128];
        ulong totalBytes = (ulong)input.Length;

        if (input.Length > 128)
        {
            int offset = 0;
            while (offset + 128 <= input.Length)
            {
                input.Slice(offset, 128).CopyTo(block);
                totalBytes = (ulong)(offset + 128);
                Compress(h, block, totalBytes, false);
                offset += 128;
            }

            block.Clear();
            input.Slice(offset).CopyTo(block);
        }
        else
        {
            block.Clear();
            input.CopyTo(block);
        }

        Compress(h, block, totalBytes, true);

        int fullWords = outputLength / 8;
        int remaining = outputLength % 8;

        for (int i = 0; i < fullWords; i++)
            BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(i * 8, 8), h[i]);

        if (remaining > 0)
        {
            Span<byte> tmp = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(tmp, h[fullWords]);
            tmp.Slice(0, remaining).CopyTo(output.Slice(fullWords * 8));
        }
    }

    public static void Blake2bLong(ReadOnlySpan<byte> input, Span<byte> output)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(lengthBytes, (uint)output.Length);

        byte[] initialInput = new byte[lengthBytes.Length + input.Length];
        lengthBytes.CopyTo(initialInput);
        input.CopyTo(initialInput.AsSpan(lengthBytes.Length));

        Span<byte> buffer = stackalloc byte[64];
        Blake2b.ComputeHash(initialInput, buffer, 64);

        if (output.Length <= 64)
        {
            buffer.Slice(0, output.Length).CopyTo(output);
            return;
        }

        buffer.CopyTo(output); // First 64 bytes
        int written = 64;

        Span<byte> tmp = stackalloc byte[64];

        while (written + 64 <= output.Length)
        {
            Blake2b.ComputeHash(buffer, tmp, 64);
            tmp.CopyTo(output.Slice(written));
            tmp.CopyTo(buffer); // update buffer
            written += 64;
        }

        if (written < output.Length)
        {
            Blake2b.ComputeHash(buffer, tmp, 64);
            tmp.Slice(0, output.Length - written).CopyTo(output.Slice(written));
        }
    }

    internal static void Compress(Span<ulong> h, ReadOnlySpan<byte> block, ulong t, bool final)
    {
        if (Avx2.IsSupported && !ForceScalar)
        {
            CompressAvx2(h, block, t, final); // dispatch to AVX2
            return;
        }

        Span<ulong> v = stackalloc ulong[16];
        Span<ulong> m = stackalloc ulong[16];

        h.CopyTo(v);
        IV.CopyTo(v.Slice(8));

        v[12] ^= t;
        if (final) v[14] ^= ~0UL;

        for (int i = 0; i < 16; i++)
            m[i] = BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(i * 8, 8));

        for (int round = 0; round < 12; round++)
        {
            G(v, 0, 4, 8, 12, m[SigmaIndex(round, 0)], m[SigmaIndex(round, 1)]);
            G(v, 1, 5, 9, 13, m[SigmaIndex(round, 2)], m[SigmaIndex(round, 3)]);
            G(v, 2, 6, 10, 14, m[SigmaIndex(round, 4)], m[SigmaIndex(round, 5)]);
            G(v, 3, 7, 11, 15, m[SigmaIndex(round, 6)], m[SigmaIndex(round, 7)]);
            G(v, 0, 5, 10, 15, m[SigmaIndex(round, 8)], m[SigmaIndex(round, 9)]);
            G(v, 1, 6, 11, 12, m[SigmaIndex(round, 10)], m[SigmaIndex(round, 11)]);
            G(v, 2, 7, 8, 13, m[SigmaIndex(round, 12)], m[SigmaIndex(round, 13)]);
            G(v, 3, 4, 9, 14, m[SigmaIndex(round, 14)], m[SigmaIndex(round, 15)]);
        }

        for (int i = 0; i < 8; i++)
            h[i] ^= v[i] ^ v[i + 8];
    }

    internal static void CompressAvx2(Span<ulong> h, ReadOnlySpan<byte> block, ulong t, bool final)
    {
        if (!Avx2.IsSupported)
            throw new PlatformNotSupportedException("AVX2 not supported");

        // Load state into vectors (lanes: a,b,c,d...)
        Vector256<ulong>[] v = new Vector256<ulong>[16];
        for (int i = 0; i < 8; i++)
            v[i] = Vector256.Create(h[i]);
        for (int i = 0; i < 8; i++)
            v[i + 8] = Vector256.Create(IV[i]);

        v[12] = Avx2.Xor(v[12], Vector256.Create(t));
        if (final)
            v[14] = Avx2.Xor(v[14], Vector256.Create(~0UL));

        Span<ulong> m = stackalloc ulong[16];
        for (int i = 0; i < 16; i++)
            m[i] = BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(i * 8, 8));

        for (int round = 0; round < 12; round++)
        {
            G(ref v[0], ref v[4], ref v[8], ref v[12], m[SigmaIndex(round, 0)], m[SigmaIndex(round, 1)]);
            G(ref v[1], ref v[5], ref v[9], ref v[13], m[SigmaIndex(round, 2)], m[SigmaIndex(round, 3)]);
            G(ref v[2], ref v[6], ref v[10], ref v[14], m[SigmaIndex(round, 4)], m[SigmaIndex(round, 5)]);
            G(ref v[3], ref v[7], ref v[11], ref v[15], m[SigmaIndex(round, 6)], m[SigmaIndex(round, 7)]);
            G(ref v[0], ref v[5], ref v[10], ref v[15], m[SigmaIndex(round, 8)], m[SigmaIndex(round, 9)]);
            G(ref v[1], ref v[6], ref v[11], ref v[12], m[SigmaIndex(round, 10)], m[SigmaIndex(round, 11)]);
            G(ref v[2], ref v[7], ref v[8], ref v[13], m[SigmaIndex(round, 12)], m[SigmaIndex(round, 13)]);
            G(ref v[3], ref v[4], ref v[9], ref v[14], m[SigmaIndex(round, 14)], m[SigmaIndex(round, 15)]);
        }

        for (int i = 0; i < 8; i++)
        {
            Vector256<ulong> result = Avx2.Xor(Avx2.Xor(v[i], v[i + 8]), Vector256.Create(h[i]));
            h[i] = result.GetElement(0); // Still use only lane 0 for output
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void G(Span<ulong> v, int a, int b, int c, int d, ulong x, ulong y)
    {
        v[a] = v[a] + v[b] + x;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 32);
        v[c] = v[c] + v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 24);
        v[a] = v[a] + v[b] + y;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 16);
        v[c] = v[c] + v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 63);
        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void G(ref Vector256<ulong> a, ref Vector256<ulong> b, ref Vector256<ulong> c, ref Vector256<ulong> d, ulong x, ulong y)
    {
        Vector256<ulong> vx = Vector256.Create(x);
        Vector256<ulong> vy = Vector256.Create(y);

        a = Avx2.Add(Avx2.Add(a, b), vx);
        d = RotateRight32(Avx2.Xor(d, a));
        c = Avx2.Add(c, d);
        b = RotateRight24(Avx2.Xor(b, c));
        a = Avx2.Add(Avx2.Add(a, b), vy);
        d = RotateRight16(Avx2.Xor(d, a));
        c = Avx2.Add(c, d);
        b = RotateRight63(Avx2.Xor(b, c));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> RotateRight16(Vector256<ulong> x)
    {
        return Avx2.Or(Avx2.ShiftRightLogical(x, 16), Avx2.ShiftLeftLogical(x, (byte)(48)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> RotateRight24(Vector256<ulong> x)
    {
        return Avx2.Or(Avx2.ShiftRightLogical(x, 24), Avx2.ShiftLeftLogical(x, (byte)(40)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> RotateRight32(Vector256<ulong> x)
    {
        return Avx2.Or(Avx2.ShiftRightLogical(x, 32), Avx2.ShiftLeftLogical(x, (byte)(32)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> RotateRight63(Vector256<ulong> x)
    {
        return Avx2.Or(Avx2.ShiftRightLogical(x, 63), Avx2.ShiftLeftLogical(x, (byte)(1)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte SigmaIndex(int round, int index)
    {
        return Sigma[round, index];
    }
}
