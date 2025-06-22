using System;
using System.Collections.Generic;

namespace HedgeFundTradingSystem
{
    public enum EventImpact { Low, Medium, High }

    public class NewsEvent
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public EventImpact Impact { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Blocks trading around high-impact events & breaking news.
    /// </summary>
    public class NewsFilterModule
    {
        private readonly List<NewsEvent> _schedule = new();
        private readonly TimeSpan _pre = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _post = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _cooldown = TimeSpan.FromMinutes(2);
        private DateTime? _lastBreak;

        public void AddScheduled(NewsEvent ev) => _schedule.Add(ev);
        public void OnBreakingNews() => _lastBreak = DateTime.UtcNow;

        public bool CanTradeNow(DateTime nowUtc)
        {
            if (_lastBreak.HasValue && nowUtc - _lastBreak < _cooldown)
                return false;

            foreach (var ev in _schedule)
            {
                if (ev.Impact != EventImpact.High) continue;
                if (nowUtc >= ev.Start - _pre && nowUtc <= ev.End + _post)
                    return false;
            }

            return true;
        }
    }
}
