using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public static class VectorHelper
    {
        public static Vector4 WithAlpha(this Color color, float alpha)
        {
            return new Vector4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, alpha);
        }
    }
}
