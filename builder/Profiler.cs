using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace builder
{
    class Profiler : IDisposable
    {
        readonly string name;
        Stopwatch timer;

        static Dictionary<string, long> results = new Dictionary<string, long>();


        public Profiler(string name)
        {
            this.name = name;
            this.timer = Stopwatch.StartNew();

            Debug.WriteLine("Starting " + name);
        }


        public void Dispose()
        {
            long previous;

            if (!results.TryGetValue(name, out previous))
            {
                previous = 0;
            }

            results[name] = previous + timer.ElapsedTicks;

            Debug.WriteLine("Finished " + name);
        }


        public static void OutputResults()
        {
            var sorted = from result in results
                         orderby result.Value descending
                         select result;

            var totalTime = sorted.First().Value;

            var seconds = totalTime / Stopwatch.Frequency;

            Debug.WriteLine("Execution time: {0}:{1}", seconds / 60, seconds % 60);

            foreach (var result in sorted)
            {
                Debug.WriteLine("{0}: {1}%", result.Key, result.Value * 100 / totalTime);
            }
        }
    }
}
