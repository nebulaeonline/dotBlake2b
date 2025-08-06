using System.Reflection;
using System.Runtime.InteropServices;

namespace nebulae.dotBlake2b
{
    internal class Blake2bLibrary
    {
        private static bool _isLoaded;

        internal static void Init()
        {
            if (_isLoaded)
                return;

            NativeLibrary.SetDllImportResolver(typeof(Blake2bLibrary).Assembly, Resolve);

            _isLoaded = true;
        }

        private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != "blake2b")
                return IntPtr.Zero;

            var libName = GetPlatformLibraryName();
            var assemblyDir = Path.GetDirectoryName(typeof(Blake2bLibrary).Assembly.Location)!;
            var fullPath = Path.Combine(assemblyDir, libName);

            if (!File.Exists(fullPath))
                throw new DllNotFoundException($"Could not find native Blake2b library at {fullPath}");

            return NativeLibrary.Load(fullPath);
        }

        private static string GetPlatformLibraryName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine("runtimes", "win-x64", "native", "blake2b.dll");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Path.Combine("runtimes", "linux-x64", "native", "libblake2b.so");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    return Path.Combine("runtimes", "osx-arm64", "native", "libblake2b.dylib");

                return Path.Combine("runtimes", "osx-x64", "native", "libblake2b.dylib");
            }

            throw new PlatformNotSupportedException("Unsupported platform");
        }
    }
}
