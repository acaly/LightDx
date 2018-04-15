using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal static class StructArrayHelper<T>
    {
        //Device
        public unsafe delegate uint CreateBufferDelegate_SetPtr(IntPtr @this,
            void* d, void* data, out IntPtr r, ref T dataSrc);
        //DeviceContext
        public unsafe delegate void UpdateSubresourceDelegate(IntPtr @this,
            IntPtr r, int subres, uint* box, ref T data, int p1, int p2);

        public static readonly CreateBufferDelegate_SetPtr CreateBuffer;
        public static readonly UpdateSubresourceDelegate UpdateSubresource;
        public delegate void CopyArrayDelegate(IntPtr ptr, T[] array, int offsetBytes, int nbytes);
        public static readonly CopyArrayDelegate CopyArray;
        public delegate void CopyStructDelegate(IntPtr ptr, ref T data, int offsetBytes, int nbytes);
        public static readonly CopyStructDelegate CopyStruct;
        
        static StructArrayHelper()
        {
            CreateBuffer = CalliGenerator.GetCalliDelegate_Device_CreateBuffer
                <CreateBufferDelegate_SetPtr, T>(3, 2);
            UpdateSubresource = CalliGenerator.GetCalliDelegate_PinRef
                <UpdateSubresourceDelegate, T>(48, 4);
            CopyArray = CalliGenerator.GenerateMemCopy<CopyArrayDelegate, T[]>();
            CopyStruct = CalliGenerator.GenerateMemCopy<CopyStructDelegate, T>();
        }
    }
}
