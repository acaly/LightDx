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

        internal static readonly IntPtr POSITION = Marshal.StringToHGlobalAnsi("POSITION");
        internal static readonly IntPtr COLOR = Marshal.StringToHGlobalAnsi("COLOR");
        internal static readonly IntPtr TEXCOORD = Marshal.StringToHGlobalAnsi("TEXCOORD");
    }

    public class PositionAttribute : InputAttribute
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

    //Only supports R32G32B32A32_Float color.
    public class ColorAttribute : InputAttribute
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

    public class TexCoordAttribute : InputAttribute
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
}
