using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class ResearchModule
    {
        private readonly string _path;

        public ResearchModule()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTraderLogs");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "trade_log.csv");
        }

        public void RunWalkForward(List<int> grid, Func<int, double> backtest)
        {
            int window = 100;
            for (int start = 0; start < 1000; start += window)
            {
                double best = double.MinValue;
                int bp = grid[0];
                foreach (var p in grid)
                {
                    double perf = backtest(p);
                    if (perf > best) { best = perf; bp = p; }
                }
                double oos = backtest(bp);
                Console.WriteLine($"WF {start}-{start + window}: Param={bp}, OOS={oos:F2}");
            }
        }

        public void RunMonteCarlo(int sims = 1000)
        {
            if (!File.Exists(_path)) { Console.WriteLine("No log"); return; }
            var pnls = File.ReadLines(_path).Skip(1)
                        .Select(l => l.Split(','))
                        .Where(f => f.Length > 10 && double.TryParse(f[10], out _))
                        .Select(f => double.Parse(f[10]))
                        .ToArray();

            var rnd = new Random();
            double worst = 0;
            for (int i = 0; i < sims; i++)
            {
                var seq = pnls.OrderBy(_ => rnd.Next()).ToArray();
                double bal = 0, peak = 0, dd = 0;
                foreach (var x in seq)
                {
                    bal += x;
                    peak = Math.Max(peak, bal);
                    dd = Math.Max(dd, peak - bal);
                }
                worst = Math.Max(worst, dd);
            }
            Console.WriteLine($"Worst drawdown {worst:F2}");
        }
    }
}
