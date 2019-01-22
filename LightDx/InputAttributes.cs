using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx.InputAttributes
{
    public abstract class InputAttribute : Attribute
    {
        internal abstract IntPtr SemanticName { get; }
        internal int SemanticIndex { get; set; }

        public int Format { get; set; }

        internal static readonly IntPtr POSITION = Marshal.StringToHGlobalAnsi("POSITION");
        internal static readonly IntPtr COLOR = Marshal.StringToHGlobalAnsi("COLOR");
        internal static readonly IntPtr TEXCOORD = Marshal.StringToHGlobalAnsi("TEXCOORD");
        internal static readonly IntPtr NORMAL = Marshal.StringToHGlobalAnsi("NORMAL");
        internal static readonly IntPtr TANGENT = Marshal.StringToHGlobalAnsi("TANGENT");
        internal static readonly IntPtr BINORMAL = Marshal.StringToHGlobalAnsi("BINORMAL");
    }

    public sealed class PositionAttribute : InputAttribute
    {
        public PositionAttribute(int id = 0)
        {
            SemanticIndex = id;
        }

        internal override IntPtr SemanticName
        {
            get { return POSITION; }
        }
    }

    public sealed class ColorAttribute : InputAttribute
    {
        public ColorAttribute(int id = 0)
        {
            SemanticIndex = id;
        }

        internal override IntPtr SemanticName
        {
            get { return COLOR; }
        }
    }

    public sealed class TexCoordAttribute : InputAttribute
    {
        public TexCoordAttribute(int id = 0)
        {
            SemanticIndex = id;
        }

        internal override IntPtr SemanticName
        {
            get { return TEXCOORD; }
        }
    }

    public sealed class NormalAttribute : InputAttribute
    {
        public NormalAttribute(int id = 0)
        {
            SemanticIndex = id;
        }

        internal override IntPtr SemanticName
        {
            get { return NORMAL; }
        }
    }

    public sealed class TangentAttribute : InputAttribute
    {
        public TangentAttribute(int id = 0)
        {
            SemanticIndex = id;
        }

        internal override IntPtr SemanticName
        {
            get { return TANGENT; }
        }
    }

    public sealed class BinormalAttribute : InputAttribute
    {
        public BinormalAttribute(int id = 0)
        {
            SemanticIndex = id;
        }

        internal override IntPtr SemanticName
        {
            get { return BINORMAL; }
        }
    }
}
