using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal static class CompilerStringConstants
    {
        public static readonly IntPtr VS = Ptr("VS");
        public static readonly IntPtr GS = Ptr("GS");
        public static readonly IntPtr PS = Ptr("PS");
        public static readonly IntPtr vs_4_0 = Ptr("vs_4_0");
        public static readonly IntPtr gs_4_0 = Ptr("gs_4_0");
        public static readonly IntPtr ps_4_0 = Ptr("ps_4_0");

        private unsafe static IntPtr Ptr(string str)
        {
            return Marshal.StringToHGlobalAnsi(str);
        }
    }

    internal static class Guids
    {
        public static readonly IntPtr Texture2D = Allocate("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        public static readonly IntPtr Factory = Allocate("7b7166ec-21c7-44ae-b21a-c9ae321ae369");

        private static IntPtr Allocate(string guid)
        {
            IntPtr ret = Marshal.AllocHGlobal(16);
            Marshal.StructureToPtr(new Guid(guid), ret, false);
            return ret;
        }
    }
}
