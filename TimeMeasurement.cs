using System;
using System.Diagnostics;

namespace GooglePlayStoreCrawler
{
    public class TimeMeasurement
    {
        private Stopwatch stopwatch;

        public void Start()
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        public void Stop()
        {
            if (stopwatch != null)
            {
                stopwatch.Stop();
            }
        }

        public long GetElapsedTimeMilliseconds()
        {
            return stopwatch != null ? stopwatch.ElapsedMilliseconds : 0;
        }
    }
}
