using System;
using System.Collections.Generic;
using System.Linq;
using Munion25.AppHost.MUN25.HedgeFundTradingSystem;
using NinjaTrader.NinjaScript;

namespace HedgeFundTradingSystem
{
    public class TradeSignal
    {
        public bool GoLong { get; set; }
        public bool GoShort { get; set; }
        public double Stop { get; set; }
        public double Target { get; set; }
    }

    public class TradeSignalLogic
    {
        private readonly SREngine _sr;
        private readonly RegimeDetector _rd;
        private readonly OrderFlowModule _of;
        private readonly NewsFilterModule _nf;

        public double BreakoutThreshold { get; set; } = 0.001;
        public double ConfluenceTolerance { get; set; } = 0.002;

        public TradeSignalLogic(
            SREngine sr,
            RegimeDetector rd,
            OrderFlowModule of,
            NewsFilterModule nf
        )
        {
            _sr = sr;
            _rd = rd;
            _of = of;
            _nf = nf;
        }

        public TradeSignal Evaluate(Strategy strat, string symbol, int[] timeFrames)
        {
            var sig = new TradeSignal();
            var now = strat.Time[0].ToUniversalTime();
            if (!_nf.CanTradeNow(now)) return sig;

            var regime = _rd.GetCurrentRegime(symbol, timeFrames);
            bool isBull = regime.Label == "Uptrend";
            bool isBear = regime.Label == "Downtrend";
            if (!isBull && !isBear) return sig;

            double price = strat.Close[0];
            int tf = strat.BarsPeriod.Value;

            // get last swings on this TF
            var swings = _sr.CalculateSwings(strat, symbol, timeFrames)
                            .Where(z => z.TimeframeMinutes == tf)
                            .ToList();
            if (swings.Count < 2) return sig;

            double high = swings.Where(z => z.Type == "SwingHigh").Max(z => z.Price);
            double low = swings.Where(z => z.Type == "SwingLow").Min(z => z.Price);

            // volume zones
            var vz = _of.ComputeVolumeProfile(strat, symbol, 0);
            double vah = vz.FirstOrDefault(z => z.Type == "VAH")?.Price ?? price;
            double val = vz.FirstOrDefault(z => z.Type == "VAL")?.Price ?? price;

            bool breakout = isBull
                ? price > high * (1 + BreakoutThreshold)
                : price < low * (1 - BreakoutThreshold);

            bool confl = isBull
                ? Math.Abs(price - val) / val < ConfluenceTolerance
                : Math.Abs(price - vah) / vah < ConfluenceTolerance;

            if (breakout && confl)
            {
                sig.GoLong = isBull;
                sig.GoShort = isBear;
            }

            sig.Stop = strat.Low[0] - 4 * strat.TickSize;
            sig.Target = strat.High[0] + 8 * strat.TickSize;
            return sig;
        }
    }
}
