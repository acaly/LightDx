using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal class ComScopeGuard : IDisposable
    {
        public IntPtr Ptr;

        public ComScopeGuard()
        {
        }

        public ComScopeGuard(IntPtr ptr)
        {
            Ptr = ptr;
        }

        public void Dispose()
        {
            if (Ptr != IntPtr.Zero)
            {
                Marshal.Release(Ptr);
                Ptr = IntPtr.Zero;
            }
        }

        public IntPtr Move()
        {
            var ret = Ptr;
            Ptr = IntPtr.Zero;
            return ret;
        }
    }
}
