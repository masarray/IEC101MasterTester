using System;
using System.Collections.Generic;

namespace IEC101MasterTester.ViewModels
{
    internal sealed class CommandTransaction
    {
        public string CommandIoa { get; set; }
        public string CommandType { get; set; }
        public string Operation { get; set; }
        public string Mode { get; set; }
        public string Phase { get; set; }
        public DateTime IssuedAtUtc { get; set; }
        public DateTime TxTimeUtc { get; set; }
        public DateTime? TrackerUpdatedAtUtc { get; set; }
        public DateTime? UiPublishedAtUtc { get; set; }
        public DateTime? ResponsePublishedAtUtc { get; set; }
        public DateTime TimeoutAtUtc { get; set; }
        public DateTime? ConfirmTimeUtc { get; set; }
        public double? ConfirmLatencyMs { get; set; }
        public bool IsTimedOut { get; set; }

        public bool IsClosed
        {
            get
            {
                return ConfirmTimeUtc.HasValue || IsTimedOut;
            }
        }
    }

    internal sealed class CommandLifeTrackerEngine
    {
        private readonly List<CommandTransaction> _transactions = new List<CommandTransaction>();
        private readonly object _syncRoot = new object();

        public CommandTransaction RegisterTx(string ioa, string commandType, string operation, string mode, DateTime issuedAtUtc, TimeSpan timeout)
        {
            lock (_syncRoot)
            {
                CommandTransaction transaction = new CommandTransaction
                {
                    CommandIoa = ioa ?? "-",
                    CommandType = commandType ?? "Command",
                    Operation = operation ?? string.Empty,
                    Mode = mode ?? "DO",
                    Phase = GetTxPhase(mode),
                    IssuedAtUtc = issuedAtUtc,
                    TxTimeUtc = issuedAtUtc,
                    TimeoutAtUtc = issuedAtUtc.Add(timeout),
                    ConfirmTimeUtc = null,
                    ConfirmLatencyMs = null,
                    IsTimedOut = false
                };

                _transactions.Add(transaction);
                return transaction;
            }
        }

        public CommandTransaction TryResolveRx(string ioa, string commandType, string operation, string mode, DateTime rxTimeUtc, bool isNegative)
        {
            lock (_syncRoot)
            {
                CommandTransaction match = null;

                if (!string.IsNullOrWhiteSpace(mode))
                {
                    match = FindActive(ioa, commandType, operation, mode, true);
                }

                if (match == null)
                {
                    match = FindActive(ioa, commandType, operation, mode, false);
                }

                if (match == null)
                {
                    match = FindFallback(commandType, operation, mode);
                }

                if (match == null)
                {
                    return null;
                }

                if (match.ConfirmTimeUtc.HasValue || match.IsTimedOut)
                {
                    return null;
                }

                match.ConfirmTimeUtc = rxTimeUtc;
                match.TrackerUpdatedAtUtc = rxTimeUtc;
                match.ConfirmLatencyMs = (rxTimeUtc - match.TxTimeUtc).TotalMilliseconds;
                match.Phase = GetRxPhase(match.Mode, isNegative);
                return match;
            }
        }

        public List<CommandTransaction> GetTimedOutTransactions(DateTime nowUtc)
        {
            lock (_syncRoot)
            {
                List<CommandTransaction> timedOut = new List<CommandTransaction>();

                for (int index = 0; index < _transactions.Count; index++)
                {
                    CommandTransaction transaction = _transactions[index];
                    if (transaction.IsClosed || nowUtc < transaction.TimeoutAtUtc)
                    {
                        continue;
                    }

                    transaction.IsTimedOut = true;
                    transaction.Phase = GetTimeoutPhase(transaction.Mode);
                    timedOut.Add(transaction);
                }

                return timedOut;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _transactions.Clear();
            }
        }

        private CommandTransaction FindActive(string ioa, string commandType, string operation, string mode, bool preferExactMode)
        {
            for (int index = _transactions.Count - 1; index >= 0; index--)
            {
                CommandTransaction candidate = _transactions[index];
                if (candidate.IsClosed)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(ioa)
                    && ioa != "-"
                    && !string.Equals(candidate.CommandIoa, ioa, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(candidate.CommandType, commandType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(operation)
                    && operation != "-"
                    && !string.Equals(candidate.Operation, operation, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (preferExactMode
                    && !string.IsNullOrWhiteSpace(mode)
                    && !string.Equals(candidate.Mode, mode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private CommandTransaction FindFallback(string commandType, string operation, string mode)
        {
            CommandTransaction singleMatch = null;
            int matchCount = 0;

            for (int index = _transactions.Count - 1; index >= 0; index--)
            {
                CommandTransaction candidate = _transactions[index];
                if (candidate.IsClosed)
                {
                    continue;
                }

                if (!string.Equals(candidate.CommandType, commandType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(operation)
                    && operation != "-"
                    && !string.Equals(candidate.Operation, operation, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(mode)
                    && !string.Equals(candidate.Mode, mode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchCount++;
                if (singleMatch == null)
                {
                    singleMatch = candidate;
                }

                if (matchCount > 1)
                {
                    return null;
                }
            }

            return singleMatch;
        }

        private static string GetTxPhase(string mode)
        {
            switch (mode)
            {
                case "SBO Select":
                    return "SelectTransmitted";
                case "SBO Execute":
                    return "ExecuteTransmitted";
                default:
                    return "Transmitted";
            }
        }

        private static string GetRxPhase(string mode, bool isNegative)
        {
            switch (mode)
            {
                case "SBO Select":
                    return isNegative ? "SelectRejected" : "SelectConfirmed";
                case "SBO Execute":
                    return isNegative ? "ExecuteRejected" : "ExecuteConfirmed";
                default:
                    return isNegative ? "Rejected" : "Confirmed";
            }
        }

        private static string GetTimeoutPhase(string mode)
        {
            switch (mode)
            {
                case "SBO Select":
                    return "SelectTimeout";
                case "SBO Execute":
                    return "ExecuteTimeout";
                default:
                    return "Timeout";
            }
        }
    }
}
