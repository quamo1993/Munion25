using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class PortfolioManager
    {
        private readonly Strategy _strat;
        private readonly double _perPos, _total;

        public PortfolioManager(Strategy strat, double perPos = 0.1, double total = 0.3)
        {
            _strat = strat;
            _perPos = perPos;
            _total = total;
        }

        public bool CheckExposure()
        {
            double eq = _strat.Account.Get(AccountItem.CashValue, Cbi.Currency.UsDollar);
            double tot = 0;
            foreach (var p in _strat.Account.Positions)
            {
                if (p.MarketPosition == MarketPosition.Flat) continue;
                double notional = Math.Abs(p.Quantity * p.AveragePrice);
                double exp = notional / eq;
                if (exp > _perPos) return false;
                tot += exp;
            }
            return tot <= _total;
        }
    }
}
