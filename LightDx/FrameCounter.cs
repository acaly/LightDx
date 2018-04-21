using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public sealed class FrameCounter
    {
        public FrameCounter()
        {
            UpdateMaxTime = 5000;
            UpdateMinTime = 50;
            UpdateFrame = 30;

            _count = 0;
            _clock = new Stopwatch();
        }
        
        public int UpdateMaxTime { get; set; }
        public int UpdateMinTime { get; set; }
        public int UpdateFrame { get; set; }
        private int _count;
        private long _totalCount;
        private Stopwatch _clock;
        private long _lastTick;
        private long _fpsStartTick;

        public long CountNumber => _totalCount;

        public void Start()
        {
            _clock.Start();
        }

        public float NextFrame()
        {
            _count += 1;
            _totalCount += 1;
            var tick = _clock.ElapsedTicks;
            var ret = (tick - _lastTick) / (float)TimeSpan.TicksPerMillisecond;
            _lastTick = tick;

            var ms = (tick - _fpsStartTick) / TimeSpan.TicksPerMillisecond;
            if (ms > UpdateMaxTime || _count >= UpdateFrame && ms > UpdateMinTime)
            {
                Update();
            }

            return ret;
        }

        private void Update()
        {
            Fps = (float)_count / (_clock.ElapsedTicks - _fpsStartTick) * TimeSpan.TicksPerSecond;
            _count = 0;
            _fpsStartTick = _clock.ElapsedTicks;
        }

        public float Fps { get; private set; }
    }
}
