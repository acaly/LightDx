using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal static class NativeHelper
    {
        public static int Dispose(ref IntPtr obj)
        {
            int ret = 0;
            if (obj != IntPtr.Zero)
            {
                ret = Marshal.Release(obj);
                obj = IntPtr.Zero;
            }
            return ret;
        }

        public static IntPtr AddRef(this IntPtr comObj)
        {
            if (comObj != IntPtr.Zero)
            {
                Marshal.AddRef(comObj);
            }
            return comObj;
        }
    }
}
