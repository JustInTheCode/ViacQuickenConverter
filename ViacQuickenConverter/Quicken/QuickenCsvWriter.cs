using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using ViacQuickenConverter.Formatting;
using ViacQuickenConverter.Viac;

namespace ViacQuickenConverter.Quicken
{
    public static class QuickenCsvWriter
    {
        public static void Write(List<Order> orders,
                                 List<Dividend> dividends,
                                 List<Deposit> deposits,
                                 List<Interest> interests,
                                 List<Commission> commissions,
                                 List<Merger> mergers)
        {
            var oldToNewIsinMap = CreateOldToNewIsinMap(mergers);
            var securityNameByIsin = CreateIsinSecurityNameMap(orders, dividends, mergers);
            var filePath = GetUniqueFilePath(AppContext.BaseDirectory, $"viac_quicken_{DateTime.Now.ToString(DateFormats.Standard)}", ".csv");
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            var rows = new List<QuickenCsvRow>();
            rows.AddRange(ConvertOrders(orders, securityNameByIsin, oldToNewIsinMap));
            rows.AddRange(ConvertDividends(dividends, securityNameByIsin, oldToNewIsinMap));
            rows.AddRange(ConvertDeposits(deposits));
            rows.AddRange(ConvertInterests(interests));
            rows.AddRange(ConvertCommissions(commissions));
            csv.WriteRecords(rows);

            Console.WriteLine($"Rows written: {rows.Count}");
            Console.WriteLine($"File saved to: {filePath}");
        }

        private static Dictionary<string, string>? CreateOldToNewIsinMap(List<Merger> mergers)
        {
            if (mergers.Count == 0)
            {
                return null;
            }

            var oldToNewIsinMap = mergers.ToDictionary(kvp => kvp.OldIsin, kvp => kvp.NewIsin);
            var oldToNewestIsinMap = new Dictionary<string, string>(mergers.Count);
            foreach (var (oldIsin, newIsin) in oldToNewIsinMap)
            {
                oldToNewestIsinMap.Add(oldIsin, GetNewestIsin(oldToNewIsinMap, newIsin));
            }

            return oldToNewestIsinMap;

            static string GetNewestIsin(Dictionary<string, string> oldToNewIsinMap, string newIsin)
            {
                while (true)
                {
                    if (oldToNewIsinMap.TryGetValue(newIsin, out var newerIsin))
                    {
                        newIsin = newerIsin;
                    }
                    else
                    {
                        return newIsin;
                    }
                }
            }
        }

        private static Dictionary<string, string> CreateIsinSecurityNameMap(List<Order> orders, List<Dividend> dividends, List<Merger> mergers)
        {
            var securityNameAndDateByIsin = new Dictionary<string, (string SecurityName, DateTime Date)>();
            foreach (var order in orders)
            {
                UpdateSecurityName(securityNameAndDateByIsin, order.Isin, order.SecurityName, order.Date);
            }

            foreach (var dividend in dividends)
            {
                UpdateSecurityName(securityNameAndDateByIsin, dividend.Isin, dividend.SecurityName, dividend.Date);
            }

            foreach (var merger in mergers)
            {
                UpdateSecurityName(securityNameAndDateByIsin, merger.NewIsin, merger.NewSecurityName, merger.Date);
            }

            var securityNameByIsin = securityNameAndDateByIsin.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.SecurityName);

            return securityNameByIsin;

            static void UpdateSecurityName(Dictionary<string, (string SecurityName, DateTime Date)> isinNameMap,
                                           string transactionIsin,
                                           string transactionSecurityName,
                                           DateTime transactionDate)
            {
                if (!isinNameMap.TryGetValue(transactionIsin, out var value) || (transactionDate > value.Date && transactionSecurityName != value.SecurityName))
                {
                    isinNameMap[transactionIsin] = (transactionSecurityName, transactionDate);
                }
            }
        }

        private static string GetUniqueFilePath(string directory, string baseName, string extension)
        {
            var filePath = Path.Combine(directory, $"{baseName}{extension}");
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            var count = 0;
            do
            {
                filePath = Path.Combine(directory, $"{baseName}_{++count}{extension}");
            }
            while (File.Exists(filePath));

            Console.WriteLine($"A file named '{baseName}{extension}' already exists. The name has been changed to '{baseName}_{count}{extension}' to avoid overwriting.");

            return filePath;
        }

        private static IEnumerable<QuickenCsvRow> ConvertOrders(List<Order> orders, Dictionary<string, string> securityNameByIsin, Dictionary<string, string>? oldToNewIsinMap)
        {
            foreach (var order in orders)
            {
                string isin;
                if (oldToNewIsinMap != null && oldToNewIsinMap.TryGetValue(order.Isin, out var newIsin))
                {
                    isin = newIsin;
                }
                else
                {
                    isin = order.Isin;
                }

                yield return new QuickenCsvRow
                             {
                                 Action = order.Type == OrderType.Buy ? "Bought" : "Sold",
                                 Date = order.Date.ToString(DateFormats.Standard),
                                 Account = $"VIAC 3a ({order.PortfolioNumber})",
                                 Security = securityNameByIsin[isin],
                                 OptionalSymbol = isin,
                                 Shares = order.Units,
                                 Price = order.Price,
                                 Amount = order.Amount,
                                 Memo = order.Remark,
                             };
            }
        }

        private static IEnumerable<QuickenCsvRow> ConvertDividends(List<Dividend> dividends,
                                                                   Dictionary<string, string> securityNameByIsin,
                                                                   Dictionary<string, string>? oldToNewIsinMap)
        {
            foreach (var dividend in dividends)
            {
                string isin;
                if (oldToNewIsinMap != null && oldToNewIsinMap.TryGetValue(dividend.Isin, out var newIsin))
                {
                    isin = newIsin;
                }
                else
                {
                    isin = dividend.Isin;
                }

                yield return new QuickenCsvRow
                             {
                                 Action = "Div",
                                 Date = dividend.Date.ToString(DateFormats.Standard),
                                 Account = $"VIAC 3a ({dividend.PortfolioNumber})",
                                 Security = securityNameByIsin[isin],
                                 OptionalSymbol = isin,
                                 Shares = dividend.Units,
                                 Price = dividend.Payment,
                                 Amount = dividend.Amount,
                                 Memo = dividend.Remark,
                             };
            }
        }

        private static IEnumerable<QuickenCsvRow> ConvertDeposits(List<Deposit> deposits)
        {
            return deposits.Select(deposit => new QuickenCsvRow
                                              {
                                                  Action = "Cash",
                                                  Date = deposit.Date.ToString(DateFormats.Standard),
                                                  Account = $"VIAC 3a ({deposit.PortfolioNumber})",
                                                  Amount = deposit.Payment,
                                                  Memo = deposit.Remark,
                                              });
        }

        private static IEnumerable<QuickenCsvRow> ConvertInterests(List<Interest> interests)
        {
            return interests.Select(interest => new QuickenCsvRow
                                                {
                                                    Action = "IntInc",
                                                    Date = interest.Date.ToString(DateFormats.Standard),
                                                    Account = $"VIAC 3a ({interest.PortfolioNumber})",
                                                    Security = "Cash", // Quicken does not allow IntInc without a security name
                                                    Amount = interest.Credit,
                                                    Memo = interest.Remark,
                                                });
        }

        private static IEnumerable<QuickenCsvRow> ConvertCommissions(List<Commission> commissions)
        {
            return commissions.Select(commission => new QuickenCsvRow
                                                    {
                                                        Action = "MiscExp",
                                                        Date = commission.Date.ToString(DateFormats.Standard),
                                                        Account = $"VIAC 3a ({commission.PortfolioNumber})",
                                                        Amount = commission.ChargedAmount,
                                                        Memo = commission.Remark,
                                                        Category = "Financial:Financial Advisor",
                                                    });
        }
    }
}