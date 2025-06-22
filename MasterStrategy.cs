#region Using declarations
using System;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Security.Principal;
using System.Xml.Linq;
using HedgeFundTradingSystem;
using Munion25.AppHost.MUN25.HedgeFundTradingSystem;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class MasterStrategy : Strategy
    {
        #region Parameters
        [NinjaScriptProperty] public double RiskPerTradePercent { get; set; } = 0.01;
        [NinjaScriptProperty] public int AtrPeriod { get; set; } = 14;
        [NinjaScriptProperty] public int[] SwingLookbacks { get; set; } = new[] { 20, 50, 100, 200 };
        [NinjaScriptProperty] public int[] TimeFrames { get; set; } = new[] { 1, 5, 15, 60 };
        [NinjaScriptProperty] public string TradingHoursString { get; set; } = "CME US Index Futures RTH";
        #endregion

        private MarketDataHandler _mdh;
        private NewsFilterModule _nf;
        private SREngine _sr;
        private RegimeDetector _rd;
        private OrderFlowModule _of;
        private TradeSignalLogic _ts;
        private RiskManagementModule _rm;
        private LoggingAnalyticsModule _lg;
        private ResearchModule _rs;
        private PortfolioManager _pm;
        private double _initEq;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "MasterStrategy";
                Calculate = Calculate.OnBarClose;
                BarsRequiredToTrade = SwingLookbacks.Max() + 5;
                TradingHours = TradingHoursString;
            }
            else if (State == State.Configure)
            {
                // primary plus MTF
                InstrumentName = Instrument.FullName.StartsWith("MES")
                                 ? Instrument.FullName
                                 : "MES 09-25";

                _mdh = new MarketDataHandler(TimeFrames);
                _mdh.Configure(this);

                _nf = new NewsFilterModule();
                _sr = new SREngine(_mdh, SwingLookbacks);
                _rd = new RegimeDetector(this, _mdh, TimeFrames, new[] { 14 }, new[] { 14 });
                _of = new OrderFlowModule(_mdh);
                _ts = new TradeSignalLogic(_sr, _rd, _of, _nf);

                _rm = new RiskManagementModule(this, RiskPerTradePercent, AtrPeriod);
                _lg = new LoggingAnalyticsModule();
                _rs = new ResearchModule();
                _pm = new PortfolioManager(this, 0.1, 0.3);
            }
            else if (State == State.DataLoaded)
            {
                _initEq = Account.Get(AccountItem.CashValue, Cbi.Currency.UsDollar);
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0 || CurrentBar < BarsRequiredToTrade)
                return;

            double equity = Account.Get(AccountItem.CashValue, Cbi.Currency.UsDollar);
            if (_rm.IsRiskBreached(_initEq))
            {
                Print("[KillSwitch] disabling");
                Disable();
                return;
            }

            var sig = _ts.Evaluate(this, Instrument.FullName, TimeFrames);
            if (!sig.GoLong && !sig.GoShort) return;

            int qty = _rm.CalculatePositionSize(equity);
            if (qty <= 0 || !_pm.CheckExposure()) return;

            double entry = Close[0];
            var (stop, target) = _rm.CalculateStopsAndTargets(entry);

            if (sig.GoLong) EnterLong(qty, "LongEntry");
            if (sig.GoShort) EnterShort(qty, "ShortEntry");

            int swings = _sr.CalculateSwings(this, Instrument.FullName, TimeFrames).Count;
            int zones = _of.ComputeVolumeProfile(this, Instrument.FullName, 0).Count;
            string regime = _rd.GetCurrentRegime(Instrument.FullName, TimeFrames).Label;

            _lg.LogTrade(
                Time[0], Instrument.FullName,
                sig.GoLong ? "Long" : "Short",
                entry, qty, stop, target,
                regime, swings, zones, 0
            );
        }
    }
}
