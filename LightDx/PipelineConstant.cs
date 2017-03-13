using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public abstract class AbstractPipelineConstant : IDisposable
    {
        private IntPtr _Buffer;
        public readonly int Slot;

        protected AbstractPipelineConstant(IntPtr buffer, int slot)
        {
            _Buffer = buffer;
            Slot = slot;
        }

        //we don't need destructor because Pipeline always keeps a reference to this class

        public void Dispose()
        {
            NativeHelper.Dispose(ref _Buffer);
        }
    }

    public class PipelineConstant<T> : AbstractPipelineConstant
        where T : struct
    {
        public PipelineConstant(IntPtr buffer, int slot)
            : base(buffer, slot)
        {
        }

        public T Value;

        public void Update()
        {
            throw new NotImplementedException();
        }
    }
}
