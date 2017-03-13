using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal class NativeException : Exception
    {
        public readonly uint Code;

        public NativeException(uint code)
        {
            Code = code;
        }

        public NativeException(uint code, string msg)
            : base(msg)
        {
            Code = code;
        }
    }

    internal static class NativeErrorHelper
    {
        public static void Check(this uint code)
        {
            if (code != 0)
            {
                throw new NativeException(code);
            }
        }
    }
}
