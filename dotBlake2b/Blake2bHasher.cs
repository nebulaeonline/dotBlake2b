using System;
using System.Buffers.Binary;

namespace nebulae.dotBlake2b;

public sealed class Blake2bHasher
{
    private readonly int _outputLength;
    private readonly ulong[] _h = new ulong[8];
    private readonly byte[] _buffer = new byte[128];
    private int _bufferLen;
    private ulong _t;
    private bool _finalized;

    public Blake2bHasher(int outputLength = 64)
        : this(ReadOnlySpan<byte>.Empty, outputLength) { }

    public Blake2bHasher(ReadOnlySpan<byte> key, int outputLength = 64)
    {
        if (outputLength <= 0 || outputLength > 64)
            throw new ArgumentOutOfRangeException(nameof(outputLength), "Output length must be between 1 and 64.");
        if (key.Length > 64)
            throw new ArgumentException("Key length must not exceed 64 bytes.", nameof(key));

        _outputLength = outputLength;

        Span<byte> paramBlock = stackalloc byte[64];
        paramBlock[0] = (byte)outputLength;
        paramBlock[1] = (byte)key.Length;
        paramBlock[2] = 1; // fanout
        paramBlock[3] = 1; // depth

        for (int i = 0; i < 8; i++)
        {
            ulong paramWord = BinaryPrimitives.ReadUInt64LittleEndian(paramBlock.Slice(i * 8, 8));
            _h[i] = Blake2b.IV[i] ^ paramWord;
        }

        if (key.Length > 0)
        {
            Span<byte> keyBlock = stackalloc byte[128];
            key.CopyTo(keyBlock);
            Blake2b.Compress(_h, keyBlock, _t += 128, false);
        }
    }

    public void Update(ReadOnlySpan<byte> input)
    {
        if (_finalized) throw new InvalidOperationException("Cannot update after finalization.");

        int offset = 0;

        if (_bufferLen > 0)
        {
            int toFill = 128 - _bufferLen;
            int fill = Math.Min(toFill, input.Length);
            input.Slice(0, fill).CopyTo(_buffer.AsSpan(_bufferLen));
            _bufferLen += fill;
            offset += fill;

            if (_bufferLen == 128)
            {
                Blake2b.Compress(_h, _buffer, _t += 128, false);
                _bufferLen = 0;
            }
        }

        while (offset + 128 <= input.Length)
        {
            Blake2b.Compress(_h, input.Slice(offset, 128), _t += 128, false);
            offset += 128;
        }

        if (offset < input.Length)
        {
            input.Slice(offset).CopyTo(_buffer);
            _bufferLen = input.Length - offset;
        }
    }

    public void Finalize(Span<byte> output)
    {
        if (_finalized) throw new InvalidOperationException("Already finalized.");
        if (output.Length < _outputLength)
            throw new ArgumentException("Output buffer too small", nameof(output));

        _finalized = true;

        Array.Clear(_buffer, _bufferLen, 128 - _bufferLen);
        _t += (ulong)_bufferLen;
        Blake2b.Compress(_h, _buffer, _t, true);

        int fullWords = _outputLength / 8;
        int remaining = _outputLength % 8;

        for (int i = 0; i < fullWords; i++)
            BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(i * 8, 8), _h[i]);

        if (remaining > 0)
        {
            Span<byte> tmp = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(tmp, _h[fullWords]);
            tmp.Slice(0, remaining).CopyTo(output.Slice(fullWords * 8));
        }
    }

    public void Reset()
    {
        _bufferLen = 0;
        _t = 0;
        _finalized = false;

        Span<byte> paramBlock = stackalloc byte[64];
        paramBlock[0] = (byte)_outputLength;
        paramBlock[2] = 1;
        paramBlock[3] = 1;

        for (int i = 0; i < 8; i++)
        {
            ulong paramWord = BinaryPrimitives.ReadUInt64LittleEndian(paramBlock.Slice(i * 8, 8));
            _h[i] = Blake2b.IV[i] ^ paramWord;
        }
    }
}
