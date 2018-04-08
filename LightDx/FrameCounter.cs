using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class FrameCounter
    {
        public FrameCounter()
        {
            UpdateMaxTime = 5000;
            UpdateMinTime = 50;
            UpdateFrame = 30;

            _Count = 0;
            _Clock = new Stopwatch();
        }
        
        public int UpdateMaxTime { get; set; }
        public int UpdateMinTime { get; set; }
        public int UpdateFrame { get; set; }
        private int _Count;
        private Stopwatch _Clock;
        private long _LastTick;
        private long _FpsStartTick;

        public void Start()
        {
            _Clock.Start();
        }

        public float NextFrame()
        {
            _Count += 1;
            var tick = _Clock.ElapsedTicks;
            var ret = (tick - _LastTick) / (float)TimeSpan.TicksPerMillisecond;
            _LastTick = tick;

            var ms = (tick - _FpsStartTick) / TimeSpan.TicksPerMillisecond;
            if (ms > UpdateMaxTime || _Count >= UpdateFrame && ms > UpdateMinTime)
            {
                Update();
            }

            return ret;
        }

        private void Update()
        {
            Fps = (float)_Count / (_Clock.ElapsedTicks - _FpsStartTick) * TimeSpan.TicksPerSecond;
            _Count = 0;
            _FpsStartTick = _Clock.ElapsedTicks;
        }

        public float Fps { get; private set; }
    }
}
