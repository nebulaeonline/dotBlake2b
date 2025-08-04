using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nebulae.dotBlake2b;

public static class Blake2b
{
    static Blake2b()
    {
        // Initialize the native library
        Blake2bLibrary.Init();
    }

    public static void ComputeHash(ReadOnlySpan<byte> input, Span<byte> output, int outputLength)
    {
        var hasher = new Blake2bHasher(outputLength);
        hasher.Update(input);
        hasher.Finalize(output);
    }
}
