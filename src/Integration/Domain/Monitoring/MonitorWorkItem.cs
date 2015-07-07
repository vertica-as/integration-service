﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Integration.Model;
using Vertica.Utilities_v4;
using Vertica.Utilities_v4.Patterns;

namespace Vertica.Integration.Domain.Monitoring
{
    public class MonitorWorkItem : ContextWorkItem
    {
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        private readonly Range<DateTimeOffset> _checkRange;
	    private readonly List<Tuple<Target, MonitorEntry>> _entries;
        private readonly List<ISpecification<MonitorEntry>> _ignore;
        private readonly ChainOfResponsibilityLink<MonitorEntry, Target[]> _redirects;
        private readonly List<Regex> _messageGrouping; 

	    public MonitorWorkItem(MonitorConfiguration configuration)
        {
	        if (configuration == null) throw new ArgumentNullException("configuration");

	        DateTimeOffset upperBound = Time.UtcNow;

	        if (configuration.LastRun > upperBound)
                upperBound = configuration.LastRun;

	        _checkRange = new Range<DateTimeOffset>(configuration.LastRun, upperBound);
	        _entries = new List<Tuple<Target, MonitorEntry>>();
	        _ignore = new List<ISpecification<MonitorEntry>>();
	        _redirects = ChainOfResponsibility.Empty<MonitorEntry, Target[]>();
	        _messageGrouping = new List<Regex>();
	        Configuration = configuration;
        }

        public Range<DateTimeOffset> CheckRange
		{
			get { return _checkRange; }
		}

        public MonitorConfiguration Configuration { get; private set; }

        public MonitorWorkItem AddIgnoreFilter(ISpecification<MonitorEntry> filter)
        {
            if (filter == null) throw new ArgumentNullException("filter");

            _ignore.Add(filter);

            return this;
        }

        public MonitorWorkItem AddTargetRedirect(IChainOfResponsibilityLink<MonitorEntry, Target[]> redirect)
        {
            if (redirect == null) throw new ArgumentNullException("redirect");

            _redirects.Chain(redirect);

            return this;
        }

        public MonitorWorkItem AddMessageGroupingPattern(string pattern)
        {
            if (String.IsNullOrWhiteSpace(pattern)) throw new ArgumentException(@"Value cannot be null or empty.", "pattern");

            _messageGrouping.Add(new Regex(pattern, RegexOptions.IgnoreCase));

            return this;
        }

        public MonitorWorkItem Add(MonitorEntry entry, params Target[] targets)
        {
            if (entry == null) throw new ArgumentNullException("entry");

            if (!_ignore.Any(x => x.IsSatisfiedBy(entry)))
            {
                targets = targets ?? new Target[0];

                Target[] redirects;
                if (_redirects.TryHandle(entry, out redirects))
                    targets = targets.Concat(redirects ?? new Target[0]).ToArray();

                if (targets.Length == 0)
                    targets = new[] { Target.Service };

                foreach (Target target in targets.Distinct())
                    _entries.Add(Tuple.Create(target ?? Target.Service, entry));
            }

            return this;
        }

        public void Add(DateTimeOffset dateTime, string source, string message, params Target[] targets)
        {
            Add(new MonitorEntry(dateTime, source, message), targets);
        }

        public MonitorEntry[] GetEntries(Target target)
        {
            return _entries
                .Where(x => new[] { target, Target.All }.Any(t => x.Item1.Equals(t)))
                .Select(x => x.Item2)
                .OrderByDescending(x => x.DateTime)
                .GroupBy(x => x, new MonitorEntryGrouping(_messageGrouping))
                .Select(x => x.Count() == 1 ? x.Key : Group(x.ToArray()))
                .ToArray();
        }

        public bool HasEntriesForUnconfiguredTargets(out Target[] targets)
        {
            Target[] entriesTargets = _entries.Select(x => x.Item1).Distinct().ToArray();
            Target[] configuredTargets = (Configuration.Targets ?? new MonitorTarget[0]).Cast<Target>().Distinct().ToArray();

            targets = entriesTargets
                .Except(configuredTargets.Concat(new[] { Target.All }))
                .ToArray();

            return targets.Length > 0;
        }

        private MonitorEntry Group(MonitorEntry[] entries)
        {
            var sb = new StringBuilder();

            sb.AppendLine(entries[0].Message);
            sb.AppendLine();
            sb.AppendLine(String.Format("--- Additional similar entries (Total: {0}) ---", entries.Length));
            sb.AppendLine();

            foreach (MonitorEntry entry in entries.Skip(1))
            {
                foreach (Regex grouping in _messageGrouping)
                {
                    Match match = grouping.Match(entry.Message);

                    if (match.Success)
                        sb.AppendFormat("{0} ", match.Value);
                }

                sb.AppendLine(String.Format("({0})", entry.DateTime.ToString(English)));
            }

            return new MonitorEntry(entries[0].DateTime, entries[0].Source, sb.ToString().Trim());
        }

        private class MonitorEntryGrouping : IEqualityComparer<MonitorEntry>
        {
            private readonly List<Regex> _groupings;

            public MonitorEntryGrouping(List<Regex> groupings)
            {
                _groupings = groupings;
            }

            public bool Equals(MonitorEntry x, MonitorEntry y)
            {
                if (!String.Equals(x.Source, y.Source))
                    return false;

                string xMessage = x.Message;
                string yMessage = y.Message;

                foreach (var grouping in _groupings)
                {
                    xMessage = grouping.Replace(xMessage, String.Empty);
                    yMessage = grouping.Replace(yMessage, String.Empty);
                }

                return String.Equals(xMessage, yMessage);
            }

            public int GetHashCode(MonitorEntry obj)
            {
                return default(int);
            }
        }
    }
}