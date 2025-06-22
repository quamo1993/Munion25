using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;

namespace Munion25.AppHost.MUN25.HedgeFundTradingSystem
{
    internal class MarketDataHandler
    {
    }
}

namespace Munion25.AppHost.MUN25.HedgeFundTradingSystem
{
    /// <summary>
    /// Helper to subscribe and fetch multiple minute series.
    /// </summary>
    public class MarketDataHandler
    {
        private readonly int[] _timeFrames;
        private readonly Dictionary<int, int> _tfToBarsIndex;

        public MarketDataHandler(int[] timeFrames)
        {
            _timeFrames = timeFrames ?? throw new ArgumentNullException(nameof(timeFrames));
            _tfToBarsIndex = new Dictionary<int, int>();
        }

        /// <summary>Call in Strategy.Configure()</summary>
        public void Configure(Strategy strategy)
        {
            for (int i = 0; i < _timeFrames.Length; i++)
            {
                int tf = _timeFrames[i];
                strategy.AddDataSeries(BarsPeriodType.Minute, tf);
                _tfToBarsIndex[tf] = i + 1;  // 0 = primary
            }
        }

        /// <summary>Fetch the Bars object for a given timeframe</summary>
        public Bars GetBars(Strategy strategy, int timeframe)
        {
            if (!_tfToBarsIndex.TryGetValue(timeframe, out int idx))
                throw new ArgumentException($"TimeFrame {timeframe} not configured");
            return strategy.BarsArray[idx];
        }

        /// <summary>Helper: last close on a given timeframe</summary>
        public double GetClose(Strategy strategy, int timeframe)
            => GetBars(strategy, timeframe).GetClose(0);
    }
}
