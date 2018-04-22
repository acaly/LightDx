using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public static class MatrixHelper
    {
        public static Matrix4x4 Transpose(this Matrix4x4 matrix)
        {
            return Matrix4x4.Transpose(matrix);
        }

        public static Matrix4x4 CreatePerspectiveFieldOfView(float fov, float aspectRatio, float nearPlane = 0.1f, float farPlane = 1000f)
        {
            var yScale = 1 / (float)Math.Tan(fov / 2);
            var xScale = yScale / aspectRatio;

            return new Matrix4x4
            {
                M11 = xScale,
                M22 = yScale,
                M33 = farPlane / (farPlane - nearPlane),
                M34 = 1,
                M43 = -nearPlane * farPlane / (farPlane - nearPlane)
            };
        }

        public static Matrix4x4 CreatePerspectiveFieldOfView(this LightDevice device, float fov, float nearPlane = 0.1f, float farPlane = 1000f)
        {
            return CreatePerspectiveFieldOfView(fov, device.ScreenWidth / (float)device.ScreenHeight, nearPlane, farPlane);
        }

        public static Matrix4x4 CreateLookAt(Vector3 pos, Vector3 lookAt, Vector3 up)
        {
            var zaxis = Vector3.Normalize(lookAt - pos);
            var xaxis = Vector3.Normalize(Vector3.Cross(up, zaxis));
            var yaxis = Vector3.Cross(zaxis, xaxis);

            return new Matrix4x4
            {
                M11 = xaxis.X,
                M21 = xaxis.Y,
                M31 = xaxis.Z,
                M12 = yaxis.X,
                M22 = yaxis.Y,
                M32 = yaxis.Z,
                M13 = zaxis.X,
                M23 = zaxis.Y,
                M33 = zaxis.Z,
                M41 = Vector3.Dot(xaxis, pos) * -1f,
                M42 = Vector3.Dot(yaxis, pos) * -1f,
                M43 = Vector3.Dot(zaxis, pos) * -1f,
                M44 = 1
            };
        }
    }
}
