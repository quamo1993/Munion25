using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

namespace Munion25.AppHost.MUN25.HedgeFundTradingSystem
{
    public class SRLevel
    {
        public double Price { get; set; }
        public int TimeframeMinutes { get; set; }
        public string Type { get; set; }  // "SwingHigh" or "SwingLow"
        public int Count { get; set; }
    }

    /// <summary>
    /// Calculates and clusters swing highs/lows across multiple timeframes.
    /// </summary>
    public class SREngine
    {
        private readonly MarketDataHandler _mdh;
        private readonly int[] _lookbacks;
        private readonly double _minSwingTicks;
        private readonly int _maxZonesPerTf;

        public SREngine(
            MarketDataHandler mdh,
            int[] lookbacks,
            double minSwingTicks = 2.0,
            int maxZonesPerTf = 3
        )
        {
            _mdh = mdh ?? throw new ArgumentNullException(nameof(mdh));
            _lookbacks = lookbacks ?? throw new ArgumentNullException(nameof(lookbacks));
            _minSwingTicks = minSwingTicks;
            _maxZonesPerTf = maxZonesPerTf;
        }

        /// <summary>
        /// Scan each requested timeframe for a single swing high + swing low,
        /// then return up to <paramref name="_maxZonesPerTf"/> of each, clustered.
        /// </summary>
        public List<SRLevel> CalculateSwings(
            Strategy strategy,
            string symbol,
            int[] timeFrames
        )
        {
            var rawLevels = new List<SRLevel>();

            for (int i = 0; i < timeFrames.Length; i++)
            {
                int tf = timeFrames[i];
                int lookback = i < _lookbacks.Length ? _lookbacks[i] : _lookbacks[0];

                var bars = _mdh.GetBars(strategy, tf);
                if (bars.Count < lookback)
                    continue;

                double tickSize = bars.Instrument.MasterInstrument.TickSize;
                double minRange = _minSwingTicks * tickSize;
                int start = bars.Count - lookback;

                double high = double.MinValue, low = double.MaxValue;
                for (int idx = start; idx < bars.Count; idx++)
                {
                    high = Math.Max(high, bars.GetHigh(idx));
                    low = Math.Min(low, bars.GetLow(idx));
                }

                if (high - low < minRange)
                    continue;

                rawLevels.Add(new SRLevel
                {
                    Price = high,
                    TimeframeMinutes = tf,
                    Type = "SwingHigh",
                    Count = 1
                });
                rawLevels.Add(new SRLevel
                {
                    Price = low,
                    TimeframeMinutes = tf,
                    Type = "SwingLow",
                    Count = 1
                });
            }

            // Cluster by timeframe & type, then take top N
            return rawLevels
                .GroupBy(z => (z.TimeframeMinutes, z.Type))
                .SelectMany(g => g
                    .OrderByDescending(z => z.Type == "SwingHigh" ? z.Price : -z.Price)
                    .Take(_maxZonesPerTf)
                )
                .ToList();
        }
    }
}
