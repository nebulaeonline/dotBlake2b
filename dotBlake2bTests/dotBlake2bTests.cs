using nebulae.dotBlake2b;
using System.Text;

namespace nebulae.dotBlake2bTests;

public class dotBlake2bTests
{
    private static string ToHex(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length * 2);
        foreach (var b in data)
            sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }

    [Theory]
    [InlineData("", "786a02f742015903c6c6fd852552d272912f4740e15847618a86e217f71f5419d25e1031afee585313896444934eb04b903a685b1448b755d56f701afe9be2ce")]
    [InlineData("abc", "ba80a53f981c4d0d6a2797b69f12f6e94c212f14685ac4b74b12bb6fdbffa2d17d87c5392aab792dc252d5de4533cc9518d38aa8dbf1925ab92386edd4009923")]
    [InlineData("The quick brown fox jumps over the lazy dog", "a8add4bdddfd93e4877d2746e62817b116364a1fa7bc148d95090bc7333b3673f82401cf7aa2e4cb1ecd90296e3f14cb5413f8ed77be73045b13914cdcd6a918")]
    public void Blake2b_ComputeHash_MatchesReference(string input, string expectedHex)
    {
        Span<byte> output = stackalloc byte[64];
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);

        // Test with scalar path
        Blake2b.ForceScalar = false;
        Blake2b.ComputeHash(inputBytes, output, 64);
        string actualScalar = ToHex(output);
        Assert.Equal(expectedHex, actualScalar);

        // Test with SIMD path (if available)
        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            Blake2b.ForceScalar = false;
            Blake2b.ComputeHash(inputBytes, output, 64);
            string actualSimd = ToHex(output);
            Assert.Equal(expectedHex, actualSimd);
        }
    }

    [Fact]
    public void Blake2bHasher_ChunkedInput_MatchesWholeInputHash()
    {
        const string inputText = "The quick brown fox jumps over the lazy dog";
        byte[] inputBytes = Encoding.UTF8.GetBytes(inputText);
        Span<byte> expectedHash = stackalloc byte[64];
        Span<byte> chunkedHash = stackalloc byte[64];

        // Expected: one-shot hash
        Blake2b.ComputeHash(inputBytes, expectedHash, 64);

        // Test: streaming in two chunks
        var hasher = new Blake2bHasher(64);
        hasher.Update(inputBytes.AsSpan(0, 20));
        hasher.Update(inputBytes.AsSpan(20));
        hasher.Finalize(chunkedHash);

        Assert.Equal(ToHex(expectedHash), ToHex(chunkedHash));
    }

    [Fact]
    public void Blake2bHasher_WithKey_ProducesDifferentHashThanUnkeyed()
    {
        var message = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog");
        var key = Encoding.UTF8.GetBytes("secret-key");

        Span<byte> hashUnkeyed = stackalloc byte[64];
        Span<byte> hashKeyed = stackalloc byte[64];

        // Unkeyed
        Blake2b.ComputeHash(message, hashUnkeyed, 64);

        // Keyed
        var hasher = new Blake2bHasher(key, 64);
        hasher.Update(message);
        hasher.Finalize(hashKeyed);

        Assert.False(hashUnkeyed.SequenceEqual(hashKeyed), "Keyed and unkeyed hashes should differ");
    }

    [Fact]
    public void Blake2bHasher_SameKeySameInput_ProducesSameHash()
    {
        var key = Encoding.UTF8.GetBytes("abc1234567890");
        var input = Encoding.UTF8.GetBytes("hello world");

        Span<byte> h1 = stackalloc byte[64];
        Span<byte> h2 = stackalloc byte[64];

        var hasher1 = new Blake2bHasher(key);
        hasher1.Update(input);
        hasher1.Finalize(h1);

        var hasher2 = new Blake2bHasher(key);
        hasher2.Update(input);
        hasher2.Finalize(h2);

        Assert.Equal(h1.ToArray(), h2.ToArray());
    }
}