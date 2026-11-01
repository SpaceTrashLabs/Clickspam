using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>Sends clicks on a dedicated thread at a fixed interval.</summary>
    sealed class Clicker
    {
        Thread thread;
        volatile bool running;
        long clickCount;

        public bool IsRunning => running;
        public long ClickCount => Interlocked.Read(ref clickCount);

        public void Start(TimeSpan interval, MouseButtons button, bool doubleClick, Point? target)
        {
            if (running) return;

            long intervalTicks = Math.Max(1, (long)(interval.TotalSeconds * Stopwatch.Frequency));
            GetFlags(button, out uint down, out uint up);
            int clicksPerTick = doubleClick ? 2 : 1;

            Interlocked.Exchange(ref clickCount, 0);
            running = true;
            thread = new Thread(() => Run(intervalTicks, down, up, clicksPerTick, target))
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "Clicker",
            };
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            thread?.Join(1000);
            thread = null;
        }

        void Run(long intervalTicks, uint down, uint up, int clicksPerTick, Point? target)
        {
            // The default ~15 ms timer resolution would cap the rate around 64 clicks/sec.
            Native.timeBeginPeriod(1);
            try
            {
                var clock = Stopwatch.StartNew();
                long next = 0;
                while (running)
                {
                    long now = clock.ElapsedTicks;
                    if (now >= next)
                    {
                        if (target is Point p) Native.SetCursorPos(p.X, p.Y);
                        for (int i = 0; i < clicksPerTick; i++) Native.SendClick(down, up);
                        Interlocked.Increment(ref clickCount);

                        next += intervalTicks;
                        // After a stall (system busy, sleep), resume the normal pace instead of bursting to catch up.
                        if (next <= now) next = now + intervalTicks;
                    }
                    else
                    {
                        long msLeft = (next - now) * 1000 / Stopwatch.Frequency;
                        // Sleep in short slices so Stop() stays responsive even at very slow rates.
                        if (msLeft >= 2) Thread.Sleep((int)Math.Min(msLeft - 1, 50));
                        else Thread.Yield();
                    }
                }
            }
            finally
            {
                Native.timeEndPeriod(1);
            }
        }

        static void GetFlags(MouseButtons button, out uint down, out uint up)
        {
            switch (button)
            {
                case MouseButtons.Right:
                    down = Native.MOUSEEVENTF_RIGHTDOWN; up = Native.MOUSEEVENTF_RIGHTUP; break;
                case MouseButtons.Middle:
                    down = Native.MOUSEEVENTF_MIDDLEDOWN; up = Native.MOUSEEVENTF_MIDDLEUP; break;
                default:
                    down = Native.MOUSEEVENTF_LEFTDOWN; up = Native.MOUSEEVENTF_LEFTUP; break;
            }
        }
    }
}
