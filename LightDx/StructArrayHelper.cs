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
        public static readonly Device.CreateBufferDelegate_SetPtr<T> CreateBuffer;
        public static readonly DeviceContext.UpdateSubresourceDelegate<T> UpdateSubresource;
        public delegate void CopyArrayDelegate(IntPtr ptr, T[] array, int nbytes);
        public static readonly CopyArrayDelegate CopyArray;

        static StructArrayHelper()
        {
            CreateBuffer = CalliGenerator.GetCalliDelegate_Device_CreateBuffer
                <Device.CreateBufferDelegate_SetPtr<T>, T>(3, 2);
            UpdateSubresource = CalliGenerator.GetCalliDelegate_PinArray
                <DeviceContext.UpdateSubresourceDelegate<T>, T>(48, 4);
            CopyArray = CalliGenerator.GenerateMemCopy<CopyArrayDelegate, T>();
        }
    }
}
