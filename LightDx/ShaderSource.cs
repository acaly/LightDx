using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public sealed class ShaderSource
    {
        internal byte[] Data { get; private set; }
        internal ShaderType ShaderTypes { get; private set; }

        private ShaderSource(Stream stream, ShaderType types)
        {
            stream.Seek(0, SeekOrigin.Begin);
            byte[] shaderCode = new byte[stream.Length];
            stream.Read(shaderCode, 0, shaderCode.Length);
            Data = shaderCode;
            ShaderTypes = types;
        }

        private ShaderSource(byte[] data, ShaderType types)
        {
            Data = data;
            ShaderTypes = types;
        }

        public static ShaderSource FromString(string code, ShaderType types)
        {
            return new ShaderSource(Encoding.ASCII.GetBytes(code), types);
        }

        public static ShaderSource FromFile(string filename, ShaderType types)
        {
            using (var file = File.OpenRead(filename))
            {
                return new ShaderSource(file, types);
            }
        }

        public static ShaderSource FromStream(Stream stream, ShaderType types)
        {
            return new ShaderSource(stream, types);
        }
    }
}
