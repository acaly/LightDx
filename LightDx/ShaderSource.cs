using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class ShaderSource
    {
        internal byte[] Data { get; private set; }

        private ShaderSource(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            byte[] shaderCode = new byte[stream.Length];
            stream.Read(shaderCode, 0, shaderCode.Length);
            Data = shaderCode;
        }

        private ShaderSource(byte[] data)
        {
            Data = data;
        }

        public static ShaderSource FromString(string code)
        {
            return new ShaderSource(Encoding.ASCII.GetBytes(code));
        }

        public static ShaderSource FromFile(string filename)
        {
            using (var file = File.OpenRead(filename))
            {
                return new ShaderSource(file);
            }
        }

        public static ShaderSource FromStream(Stream stream)
        {
            return new ShaderSource(stream);
        }
    }
}
