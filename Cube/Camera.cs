using LightDx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Cube
{
    class Camera
    {
        public Vector3 Position;
        public float yaw, pitch;

        private Dictionary<Keys, bool> keys;

        public Camera(Vector3 pos)
        {
            Position = pos;
        }

        private Vector3 CalcOffset()
        {
            Vector4 offset = new Vector4(-10, 0, 0, 0);
            Vector4 x = new Vector4(0, 1, 0, 0);
            offset = Vector4.Transform(offset, Matrix4x4.CreateRotationZ(yaw));
            x = Vector4.Transform(x, Matrix4x4.CreateRotationZ(yaw));
            pitch = Math.Min((float)Math.PI / 2.001f, pitch);
            pitch = Math.Max((float)Math.PI / -2.001f, pitch);
            offset = Vector4.Transform(offset, Matrix4x4.CreateFromAxisAngle(new Vector3(x.X, x.Y, x.Z), -pitch));
            return new Vector3(offset.X, offset.Y, offset.Z);
        }

        public Vector3 MoveHorizontal(Vector4 b)
        {
            Vector4 move = b;
            move = Vector4.Transform(move, Matrix4x4.CreateRotationZ(yaw));
            return new Vector3(move.X, move.Y, move.Z);
        }

        public Vector3 MoveHorizontal(float x, float y)
        {
            return MoveHorizontal(new Vector4(x, y, 0, 0));
        }

        public void SetForm(Form form)
        {
            keys = new Dictionary<Keys, bool>() {
                        { Keys.W, false }, { Keys.S, false }, { Keys.A, false }, { Keys.D, false },
                        { Keys.Up, false }, { Keys.Down, false }, { Keys.Left, false }, { Keys.Right, false },
                        { Keys.Q, false }, { Keys.E, false },
                        { Keys.Space, false }, { Keys.Z, false },
                    };
            form.KeyDown += delegate (object obj, KeyEventArgs e)
            {
                if (keys.ContainsKey(e.KeyCode)) keys[e.KeyCode] = true;
            };
            form.KeyUp += delegate (object obj, KeyEventArgs e)
            {
                if (keys.ContainsKey(e.KeyCode)) keys[e.KeyCode] = false;
            };
        }

        public void Step()
        {
            Vector3 acc;
            Vector4 movedir = new Vector4(0, 0, 0, 0);
            if (keys[Keys.W]) movedir.X -= 1;
            if (keys[Keys.S]) movedir.X += 1;
            if (keys[Keys.A]) movedir.Y += 1;
            if (keys[Keys.D]) movedir.Y -= 1;
            if (movedir.LengthSquared() > 0)
            {
                movedir = Vector4.Normalize(movedir);
                acc = MoveHorizontal(movedir);
                Position += acc * 0.05f;
            }

            if (keys[Keys.Up]) pitch -= 0.03f;
            if (keys[Keys.Down]) pitch += 0.03f;
            if (keys[Keys.Left]) yaw -= 0.03f;
            if (keys[Keys.Right]) yaw += 0.03f;

            if (keys[Keys.Space])
            {
                Position.Z += 0.01f;
            }
            else if (keys[Keys.Z])
            {
                Position.Z -= 0.05f;
            }
        }
    }
}
