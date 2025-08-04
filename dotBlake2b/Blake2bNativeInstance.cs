using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nebulae.dotBlake2b;

internal sealed class Blake2bNativeInstance
{
    private Blake2bInterop.blake2b_state _state;
    private readonly byte[]? _key;
    private readonly int _digestLen;

    public Blake2bNativeInstance(int digestLen)
    {
        _digestLen = digestLen;
        _key = null;
        Blake2bInterop.blake2b_init(out _state, digestLen);
    }

    public Blake2bNativeInstance(ReadOnlySpan<byte> key, int digestLen)
    {
        _digestLen = digestLen;
        _key = key.ToArray(); // must store to support Reset()
        Blake2bInterop.blake2b_init_key(out _state, digestLen, _key, _key.Length);
    }

    public void Update(ReadOnlySpan<byte> input)
    {
        Blake2bInterop.blake2b_update(ref _state, input.ToArray(), input.Length);
    }

    public void Finalize(Span<byte> output)
    {
        var tmp = new byte[_digestLen];
        if (Blake2bInterop.blake2b_final(ref _state, tmp, _digestLen) != 0)
            throw new InvalidOperationException("blake2b_final failed");
        tmp.CopyTo(output);
    }

    public void Reset()
    {
        if (_key != null)
        {
            Blake2bInterop.blake2b_init_key(out _state, _digestLen, _key, _key.Length);
        }
        else
        {
            Blake2bInterop.blake2b_init(out _state, _digestLen);
        }
    }
}
