using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Munion25.AppHost.MUN25.HedgeFundTradingSystem;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;

namespace HedgeFundTradingSystem
{
    /// <summary>
    /// A volume profile zone (POC, VAH, VAL).
    /// </summary>
    public class VolumeZone
    {
        public double Price { get; set; }
        public string Type { get; set; }  // "POC","VAH","VAL"
        public double Volume { get; set; }
    }

    /// <summary>
    /// Computes rolling volume profiles and identifies POC, VAH, VAL.
    /// </summary>
    public class OrderFlowModule
    {
        private readonly MarketDataHandler _mdh;
        private readonly int _bins;
        private readonly int _window;
        private readonly double _valueAreaPct;
        private readonly ConcurrentDictionary<string, double[]> _cache;

        public OrderFlowModule(MarketDataHandler mdh, int bins = 20, int window = 50, double valueAreaPct = 0.7)
        {
            _mdh = mdh ?? throw new ArgumentNullException(nameof(mdh));
            _bins = bins;
            _window = window;
            _valueAreaPct = valueAreaPct;
            _cache = new ConcurrentDictionary<string, double[]>();
        }

        /// <summary>
        /// Compute POC, VAH, VAL over the last N bars for given symbol and timeframe.
        /// </summary>
        public List<VolumeZone> ComputeVolumeProfile(Strategy strat, string symbol, int timeframe)
        {
            var bars = _mdh.GetBars(strat, timeframe);
            int cnt = bars.Count;
            if (cnt < _window)
                return new List<VolumeZone>();

            int start = cnt - _window;
            double high = double.MinValue, low = double.MaxValue;
            for (int i = start; i < cnt; i++)
            {
                high = Math.Max(high, bars.GetHigh(i));
                low = Math.Min(low, bars.GetLow(i));
            }
            double range = high - low;
            if (range <= 0)
                return new List<VolumeZone>();

            string key = $"{symbol}_{timeframe}";
            var binsArr = _cache.GetOrAdd(key, _ => new double[_bins]);
            Array.Clear(binsArr, 0, _bins);
            double binSize = range / _bins;

            // accumulate raw volume
            for (int i = start; i < cnt; i++)
            {
                double price = bars.GetClose(i);
                double vol = bars.GetVolume(i);
                int idx = (int)((price - low) / binSize);
                idx = Math.Max(0, Math.Min(_bins - 1, idx));
                binsArr[idx] += vol;
            }

            // 3-point smoothing
            var smooth = new double[_bins];
            for (int i = 0; i < _bins; i++)
            {
                double sum = 0;
                int c = 0;
                for (int k = i - 1; k <= i + 1; k++)
                    if (k >= 0 && k < _bins) { sum += binsArr[k]; c++; }
                smooth[i] = c > 0 ? sum / c : 0;
            }

            double totalVol = smooth.Sum();
            if (totalVol <= 0)
                return new List<VolumeZone>();

            int pocBin = Array.IndexOf(smooth, smooth.Max());
            double pocPrice = low + (pocBin + 0.5) * binSize;

            var sorted = smooth
                .Select((v, i) => (Idx: i, Vol: v))
                .OrderByDescending(x => x.Vol)
                .ToList();

            double targetVol = totalVol * _valueAreaPct, cum = 0;
            double vah = pocPrice, val = pocPrice;

            foreach (var e in sorted)
            {
                if (cum >= targetVol) break;
                double center = low + (e.Idx + 0.5) * binSize;
                vah = Math.Max(vah, center);
                val = Math.Min(val, center);
                cum += e.Vol;
            }

            return new List<VolumeZone>
            {
                new VolumeZone { Type = "POC", Price = pocPrice, Volume = smooth[pocBin] },
                new VolumeZone { Type = "VAH", Price = vah,      Volume = cum           },
                new VolumeZone { Type = "VAL", Price = val,      Volume = cum           }
            };
        }
    }
}
