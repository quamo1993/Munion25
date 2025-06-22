using System;
using System.Collections.Generic;
using Munion25.AppHost.MUN25.HedgeFundTradingSystem;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace HedgeFundTradingSystem
{
    public class RegimeSignal
    {
        public string Label { get; set; }  // "Uptrend","Neutral","Downtrend"
        public double Strength { get; set; }  // [-1..1]
    }

    public class RegimeDetector
    {
        private readonly Strategy _strategy;
        private readonly MarketDataHandler _mdh;
        private readonly int[] _maPeriods;
        private readonly int[] _adxPeriods;
        private readonly int[] _rsiPeriods;
        private readonly Dictionary<string, double> _weights;
        private readonly double _upTh, _downTh, _alpha;
        private double _smoothed;

        public RegimeDetector(
            Strategy strategy,
            MarketDataHandler mdh,
            int[] maPeriods,
            int[] adxPeriods,
            int[] rsiPeriods,
            Dictionary<string, double> weights = null,
            double upThreshold = 0.2,
            double downThreshold = -0.2,
            double smoothingFactor = 0.3
        )
        {
            _strategy = strategy;
            _mdh = mdh;
            _maPeriods = maPeriods;
            _adxPeriods = adxPeriods;
            _rsiPeriods = rsiPeriods;
            _weights = weights ?? new Dictionary<string, double> { { "MA", 1 }, { "ADX", 0.5 }, { "RSI", 0.5 } };
            _upTh = upThreshold;
            _downTh = downThreshold;
            _alpha = smoothingFactor;
            _smoothed = 0;
        }

        public RegimeSignal GetCurrentRegime(string symbol, int[] timeFrames)
        {
            double score = 0, weight = 0;

            // MA slopes on each TF
            foreach (int tf in _maPeriods)
            {
                var bars = _mdh.GetBars(_strategy, tf);
                if (bars.Count < tf + 1) continue;
                var series = SMA(bars, tf);
                double delta = series[0] - series[1];
                score += Math.Sign(delta) * _weights["MA"];
                weight += _weights["MA"];
            }

            // ADX on primary
            foreach (int p in _adxPeriods)
            {
                double adx = _strategy.ADX(p)[0] / 100;
                score += adx * _weights["ADX"];
                weight += _weights["ADX"];
            }

            // RSI on primary
            foreach (int p in _rsiPeriods)
            {
                double rsi = (_strategy.RSI(p, 1)[0] - 50) / 50;
                score += rsi * _weights["RSI"];
                weight += _weights["RSI"];
            }

            double raw = weight > 0 ? score / weight : 0;
            _smoothed = _alpha * raw + (1 - _alpha) * _smoothed;

            string label = _smoothed >= _upTh ? "Uptrend"
                         : _smoothed <= _downTh ? "Downtrend"
                         : "Neutral";

            return new RegimeSignal { Label = label, Strength = Math.Round(_smoothed, 3) };
        }

        // helper to compute SMA on a Bars series
        private Series<double> SMA(Bars b, int period)
            => SMA(b, period, (int?)null);

        private Series<double> SMA(Bars b, int period, int? smooth)
            => NinjaTrader.NinjaScript.Indicators.SMA(b, period).GetSeries(b);
    }
}
