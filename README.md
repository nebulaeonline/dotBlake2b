# dotBlake2b

A .NET 8+ SSE4-accelerated (x86_64) wrapper for the reference Blake2b hash function. This library is designed to be fast and efficient, leveraging the latest advancements in .NET for optimal performance.

Test & benchmark suite available on the [GitHub repository](https://github.com/nebulaeonline/dotBlake2b).

[![NuGet](https://img.shields.io/nuget/v/nebulae.dotBlake2b.svg)](https://www.nuget.org/packages/nebulae.dotBlake2b)

---

## Features

- Streaming API for hashing large data efficiently.
- MAC (Message Authentication Code) support for data integrity verification.
- **Performance**: Utilizes SSE4/neon instructions for high-speed hashing on x86_64 & aarch64 architectures.
- **Simplicity**: Easy to integrate into existing .NET applications with minimal setup.
- **Security**: Implements the Blake2b hash function, known for its security and speed.
- **No External Dependencies**: A self-contained library with no external dependencies, ensuring easy deployment.
- **Cross-Platform**: Compatible with .NET 8+ Windows & Linux x64 and MacOS (x64 & Apple Silicon).

---

## Requirements

- .NET 8 or later
- SSE4 or neon-capable CPU

---

## Usage

```csharp

using System;
using System.Text;
using nebulae.dotBlake2b;

class Program
{
    static void Main()
    {
        // 1. One-shot hash
        string input = "The quick brown fox jumps over the lazy dog";
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        Span<byte> hash1 = stackalloc byte[64];
        Blake2b.ComputeHash(inputBytes, hash1, 64);
        Console.WriteLine("One-shot hash:   " + Convert.ToHexString(hash1));

        // 2. Streaming hash in chunks
        var hasher = new Blake2bHasher(64);
        hasher.Update(inputBytes.AsSpan(0, 20));
        hasher.Update(inputBytes.AsSpan(20));
        Span<byte> hash2 = stackalloc byte[64];
        hasher.Finalize(hash2);
        Console.WriteLine("Streaming hash:  " + Convert.ToHexString(hash2));

        // 3. MAC (keyed hash)
        byte[] key = Encoding.UTF8.GetBytes("supersecretkey");
        var mac = new Blake2bHasher(key, 64);
        mac.Update(inputBytes);
        Span<byte> hash3 = stackalloc byte[64];
        mac.Finalize(hash3);
        Console.WriteLine("Keyed MAC hash:  " + Convert.ToHexString(hash3));
    }
}

```

---

## Installation

You can install the package via NuGet:

```bash

$ dotnet add package nebulae.dotBlake2b

```

Or via git:

```bash

$ git clone https://github.com/nebulaeonline/dotBlake2b.git
$ cd dotBlake2b
$ dotnet build

```

---

## License

MIT

## Roadmap

- Potential support for Blake2s in the future