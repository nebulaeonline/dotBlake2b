using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using nebulae.dotBlake2b;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;

namespace nebulae.dotBlake2b.Benchmarks;

[MemoryDiagnoser]
public class Blake2bBench
{
    private byte[] _input;
    private byte[] _output;

    [Params(64, 512, 4096, 65536)]
    public int InputSize;

    [GlobalSetup]
    public void Setup()
    {
        _input = Enumerable.Range(0, InputSize).Select(i => (byte)(i % 256)).ToArray();
        _output = new byte[64];
    }

    [Benchmark(Baseline = true)]
    public void Scalar()
    {
        Blake2b.ForceScalar = true;
        Blake2b.ComputeHash(_input, _output, 64);
    }

    [Benchmark]
    public void Avx2()
    {
        if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported)
            return;

        Blake2b.ForceScalar = false;
        Blake2b.ComputeHash(_input, _output, 64);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<Blake2bBench>();

        //var input = new byte[64];
        //var output = new byte[64];
        //var sw = Stopwatch.StartNew();
        //for (int i = 0; i < 1_000_000; i++)
        //    Blake2b.ComputeHash(input, output, 64);
        //sw.Stop();
        //Console.WriteLine($"Total: {sw.ElapsedMilliseconds} ms");
    }
}
