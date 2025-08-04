using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static nebulae.dotBlake2b.Blake2bInterop;

namespace nebulae.dotBlake2b;

public sealed class Blake2bHasher
{
    private readonly Blake2bNativeInstance _native;

    public Blake2bHasher(int outputLength = 64)
    {
        _native = new Blake2bNativeInstance(outputLength);
    }

    public Blake2bHasher(ReadOnlySpan<byte> key, int outputLength = 64)
    {
        _native = new Blake2bNativeInstance(key, outputLength);
    }

    public void Update(ReadOnlySpan<byte> input) => _native.Update(input);

    public void Finalize(Span<byte> output) => _native.Finalize(output);

    public void Reset() => _native.Reset();
}
