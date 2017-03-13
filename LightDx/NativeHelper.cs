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
        public static void Dispose(ref IntPtr obj)
        {
            if (obj != IntPtr.Zero)
            {
                Marshal.Release(obj);
                obj = IntPtr.Zero;
            }
        }

        public static void Dispose(ref IntPtr[] a)
        {
            if (a != null)
            {
                foreach (var o in a)
                {
                    Marshal.Release(o);
                }
                a = null;
            }
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
