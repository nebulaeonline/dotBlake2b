using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace nebulae.dotBlake2b
{
    internal class Blake2bInterop
    {
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct blake2b_state
        {
            public fixed ulong h[8];
            public fixed ulong t[2];
            public fixed ulong f[2];
            public fixed byte buf[256];
            public uint buflen;
            public uint outlen;
            public byte last_node;
        }

        [DllImport("blake2b", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int blake2b_init(out blake2b_state state, int outlen);

        [DllImport("blake2b", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int blake2b_init_key(out blake2b_state state, int outlen, byte[] key, int keylen);

        [DllImport("blake2b", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int blake2b_update(ref blake2b_state state, byte[] input, int inputLen);

        [DllImport("blake2b", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int blake2b_final(ref blake2b_state state, byte[] output, int outlen);
    }
}