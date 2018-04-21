using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal unsafe class DDSReader
    {
        #pragma warning disable CS0649

        struct DdsHeader
        {
            public uint Magic;
            public uint Size;
            public uint Flags;
            public uint Height;
            public uint Width;
            public uint PitchOrLinearSize;
            public uint Depth;
            public uint MipMapCount;
            public fixed uint Reserved1[11];
            public PixelFormat PixelFormat;
            public fixed uint caps[4];
            public uint Reserved2;
        };

        private struct PixelFormat
        {
            public uint Size;
            public uint Flags;
            public uint FourCC;
            public uint RGBBitCount;
            public uint RBitMask;
            public uint GBitMask;
            public uint BBitMask;
            public uint ABitMask;
        };

        private struct HeaderDx10
        {
            public uint DXGIFormat;
            public uint ResourceDimension;
            public uint MiscFlag;
            public uint ArraySize;
            public uint MiscFlags2;
        };

        #pragma warning restore CS0649

        private const uint MagicDDS = 0x20534444; //"DDS "
        private const uint MagicDXT1 = 0x31545844; //"DXT1"
        private const uint MagicDXT3 = 0x33545844; //"DXT3"
        private const uint MagicDXT5 = 0x35545844; //"DXT5"
        private const uint MagicDX10 = 0x30315844; //"DX10"

        public static bool CheckHeader(byte[] magic)
        {
            return BitConverter.ToUInt32(magic, 0) == MagicDDS;
        }

        private static readonly byte[] s_buffer = new byte[4 * 32];

        private static T Read<T>(Stream stream) where T : unmanaged
        {
            lock (s_buffer)
            {
                var r = stream.Read(s_buffer, 0, sizeof(T));
                Check(r == sizeof(T));
                fixed (byte* bufferPtr = s_buffer)
                {
                    return *(T*)bufferPtr;
                }
            }
        }

        private static void Check(bool cond)
        {
            if (!cond)
            {
                throw new IOException("Invalid DDS file");
            }
        }
        
        public static Texture2D Load(LightDevice device, Stream stream)
        {
            var header1 = Read<DdsHeader>(stream);
            Check(header1.Magic == MagicDDS);
            Check(header1.Size == 124);
            Check((header1.Flags & 7) == 7); //caps, height, width
            Check(header1.PixelFormat.Size == 32);
            uint format = 0;
            uint pitch = 0;
            if ((header1.PixelFormat.Flags & 0x40) == 0x40) //DDPF_RGB
            {
                Check((header1.PixelFormat.Flags & 1) == 1); //Contains alpha
                Check(header1.PixelFormat.ABitMask == 0xFF000000);
                Check(header1.PixelFormat.RBitMask == 0x00FF0000);
                Check(header1.PixelFormat.GBitMask == 0x0000FF00);
                Check(header1.PixelFormat.BBitMask == 0x000000FF);
                format = 87; //DXGI_FORMAT_B8G8R8A8_UNORM

                //There must be at least one way to calculate pitch (providing RGBBitCount or PitchOrLinearSize)
                if ((header1.PixelFormat.Flags & 0x40) == 0x40) //DDPF_RGB
                {
                    pitch = (header1.Width * header1.PixelFormat.RGBBitCount + 7) / 8;
                }
                else
                {
                    Check((header1.Flags & 8) == 8); //pitch
                }
            }
            else
            {
                Check((header1.PixelFormat.Flags & 0x4) == 0x4); //DDPF_FOURCC
                switch (header1.PixelFormat.FourCC)
                {
                    case MagicDXT1:
                        format = 71; //DXGI_FORMAT_BC1_UNORM
                        pitch = (header1.Width + 3) / 4 * 8;
                        break;
                    case MagicDXT3:
                        format = 74; //DXGI_FORMAT_BC2_UNORM
                        pitch = (header1.Width + 3) / 4 * 16;
                        break;
                    case MagicDXT5:
                        format = 77; //DXGI_FORMAT_BC3_UNORM
                        pitch = (header1.Width + 3) / 4 * 16;
                        break;
                    case MagicDX10:
                        {
                            var header2 = Read<HeaderDx10>(stream);
                            Check(header2.ResourceDimension == 3 /*DDS_DIMENSION_TEXTURE2D*/);
                            Check(header2.ArraySize == 1);
                            format = header2.DXGIFormat;
                        }
                        break;
                    default:
                        Check(false);
                        break;
                }
            }
            if ((header1.Flags & 8) == 8)
            {
                pitch = header1.PitchOrLinearSize;
            }
            var data = new byte[stream.Length - stream.Position];
            Check(stream.Read(data, 0, data.Length) == data.Length);
            fixed (byte* pData = data)
            {
                return device.CreateTexture2D((int)header1.Width, (int)header1.Height, (int)format,
                    new IntPtr(pData), (int)pitch, false);
            }
        }
    }
}
