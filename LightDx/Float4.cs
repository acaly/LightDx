using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public struct Float4
    {
        public float X, Y, Z, W;

        public Float4(Color color, float alpha = 1.0f)
        {
            X = color.R / 255.0f;
            Y = color.G / 255.0f;
            Z = color.B / 255.0f;
            W = alpha;
        }

        public static implicit operator Float4(Color c)
        {
            return new Float4(c);
        }

        public Float4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }
}
