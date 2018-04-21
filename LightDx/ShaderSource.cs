using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

            int start = 0;

            //Skip UTF-8 BOM
            if (stream.Length > 3)
            {
                byte[] bom = new byte[3];
                stream.Read(bom, 0, 3);
                if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                {
                    start = 3;
                }
                else
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }
            }

            byte[] shaderCode = new byte[stream.Length - start];
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

        public static ShaderSource FromResource(string name, ShaderType types)
        {
            return FromResource(Assembly.GetEntryAssembly(), name, types);
        }

        public static ShaderSource FromResource(Assembly assembly, string name, ShaderType types)
        {
            return FromStream(assembly.GetManifestResourceStream(assembly.GetName().Name + "." + name), types);
        }
    }
}
