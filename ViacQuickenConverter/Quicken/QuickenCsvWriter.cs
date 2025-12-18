using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using ViacQuickenConverter.Viac;

namespace ViacQuickenConverter.Quicken
{
    public static class QuickenCsvWriter
    {
        private const string DateFormat = "yyyy-MM-dd";

        public static void Write(List<Order> orders,
                                 List<DividendCancellation> dividendCancellations,
                                 List<Dividend> dividends,
                                 List<Deposit> deposits,
                                 List<Interest> interests,
                                 List<Commission> commissions,
                                 List<Merger> mergers)
        {
            var oldToNewIsinMap = CreateOldToNewIsinMap(mergers);
            var newestSecurityNameByIsin = CreateNewestSecurityNameMap(orders, dividendCancellations, dividends, mergers);
            var filePath = GetUniqueFilePath(AppContext.BaseDirectory, $"viac_quicken_{DateTime.Now.ToString(DateFormat)}", ".csv");
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            var rows = new List<QuickenCsvRow>();
            rows.AddRange(ConvertOrders(orders, newestSecurityNameByIsin, oldToNewIsinMap));
            rows.AddRange(ConvertDividendCancellations(dividendCancellations, newestSecurityNameByIsin, oldToNewIsinMap));
            rows.AddRange(ConvertDividends(dividends, newestSecurityNameByIsin, oldToNewIsinMap));
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
                while (oldToNewIsinMap.TryGetValue(newIsin, out var newerIsin))
                {
                    newIsin = newerIsin;
                }

                return newIsin;
            }
        }

        private static Dictionary<string, string> CreateNewestSecurityNameMap(List<Order> orders,
                                                                              List<DividendCancellation> dividendCancellations,
                                                                              List<Dividend> dividends,
                                                                              List<Merger> mergers)
        {
            var newestNameByIsin = new Dictionary<string, (string SecurityName, DateTime Date)>();
            foreach (var order in orders)
            {
                UpdateToNewestSecurityName(newestNameByIsin, order.Isin, order.SecurityName, order.Date);
            }

            foreach (var dividendCancellation in dividendCancellations)
            {
                UpdateToNewestSecurityName(newestNameByIsin, dividendCancellation.Isin, dividendCancellation.SecurityName, dividendCancellation.Date);
            }

            foreach (var dividend in dividends)
            {
                UpdateToNewestSecurityName(newestNameByIsin, dividend.Isin, dividend.SecurityName, dividend.Date);
            }

            foreach (var merger in mergers)
            {
                UpdateToNewestSecurityName(newestNameByIsin, merger.NewIsin, merger.NewSecurityName, merger.Date);
            }

            return newestNameByIsin.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.SecurityName);

            static void UpdateToNewestSecurityName(Dictionary<string, (string SecurityName, DateTime Date)> newestNameByIsin, string isin, string securityName, DateTime date)
            {
                if (!newestNameByIsin.TryGetValue(isin, out var currentNewest) || (date > currentNewest.Date && securityName != currentNewest.SecurityName))
                {
                    newestNameByIsin[isin] = (securityName, date);
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

        private static IEnumerable<QuickenCsvRow> ConvertOrders(List<Order> orders,
                                                                Dictionary<string, string> newestSecurityNameByIsin,
                                                                Dictionary<string, string>? oldToNewIsinMap)
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
                                 Date = order.Date.ToString(DateFormat),
                                 Account = $"VIAC 3a ({order.PortfolioNumber})",
                                 Security = newestSecurityNameByIsin[isin],
                                 OptionalSymbol = isin,
                                 Shares = order.Units,
                                 Price = order.Price,
                                 Amount = order.Amount,
                                 Memo = order.Remark,
                             };
            }
        }

        private static IEnumerable<QuickenCsvRow> ConvertDividendCancellations(List<DividendCancellation> dividendCancellations,
                                                                               Dictionary<string, string> newestSecurityNameByIsin,
                                                                               Dictionary<string, string>? oldToNewIsinMap)
        {
            foreach (var dividendCancellation in dividendCancellations)
            {
                string isin;
                if (oldToNewIsinMap != null && oldToNewIsinMap.TryGetValue(dividendCancellation.Isin, out var newIsin))
                {
                    isin = newIsin;
                }
                else
                {
                    isin = dividendCancellation.Isin;
                }

                yield return new QuickenCsvRow
                             {
                                 Action = "Div",
                                 Date = dividendCancellation.Date.ToString(DateFormat),
                                 Account = $"VIAC 3a ({dividendCancellation.PortfolioNumber})",
                                 Security = newestSecurityNameByIsin[isin],
                                 OptionalSymbol = isin,
                                 Amount = dividendCancellation.Amount,
                                 Memo = dividendCancellation.Remark,
                             };
            }
        }

        private static IEnumerable<QuickenCsvRow> ConvertDividends(List<Dividend> dividends,
                                                                   Dictionary<string, string> newestSecurityNameByIsin,
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
                                 Date = dividend.Date.ToString(DateFormat),
                                 Account = $"VIAC 3a ({dividend.PortfolioNumber})",
                                 Security = newestSecurityNameByIsin[isin],
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
                                                  Date = deposit.Date.ToString(DateFormat),
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
                                                    Date = interest.Date.ToString(DateFormat),
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
                                                        Date = commission.Date.ToString(DateFormat),
                                                        Account = $"VIAC 3a ({commission.PortfolioNumber})",
                                                        Amount = commission.ChargedAmount,
                                                        Memo = commission.Remark,
                                                        Category = "Financial:Financial Advisor",
                                                    });
        }
    }
}