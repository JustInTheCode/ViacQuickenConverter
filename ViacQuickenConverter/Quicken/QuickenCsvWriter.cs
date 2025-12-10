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
            var securityNameByIsin = CreateIsinSecurityNameMap(orders, dividends, mergers);
            var filePath = GetUniqueFilePath(AppContext.BaseDirectory, $"viac_quicken_{DateTime.Now.ToString(DateFormats.Standard)}", ".csv");
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            var rows = new List<QuickenCsvRow>();
            rows.AddRange(ConvertOrders(orders, securityNameByIsin));
            rows.AddRange(ConvertDividends(dividends, securityNameByIsin));
            rows.AddRange(ConvertDeposits(deposits));
            rows.AddRange(ConvertInterests(interests));
            rows.AddRange(ConvertCommissions(commissions));
            rows.AddRange(ConvertMergers(mergers, orders, securityNameByIsin));
            csv.WriteRecords(rows);

            Console.WriteLine($"Rows written: {rows.Count}");
            Console.WriteLine($"File saved to: {filePath}");
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
                UpdateSecurityName(securityNameAndDateByIsin, merger.OldIsin, merger.OldSecurityName, merger.Date);
                UpdateSecurityName(securityNameAndDateByIsin, merger.NewIsin, merger.NewSecurityName, merger.Date);
            }

            HashSet<string> uniqueSecurityNames = [];
            List<string> duplicateSecurityNames = [];
            foreach (var (securityName, _) in securityNameAndDateByIsin.Values)
            {
                if (!uniqueSecurityNames.Add(securityName))
                {
                    duplicateSecurityNames.Add(securityName);
                }
            }

            var securityNameByIsin = securityNameAndDateByIsin.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.SecurityName);
            if (duplicateSecurityNames.Count == 0)
            {
                return securityNameByIsin;
            }

            foreach (var duplicateSecurityName in duplicateSecurityNames)
            {
                DateTime? oldestDate = null;
                var isinToUpdate = string.Empty;
                foreach (var (isin, (securityName, date)) in securityNameAndDateByIsin)
                {
                    if (securityName != duplicateSecurityName)
                    {
                        continue;
                    }

                    if (oldestDate == null || date < oldestDate)
                    {
                        oldestDate = date;
                        isinToUpdate = isin;
                    }
                }

                securityNameByIsin[isinToUpdate] = $"{duplicateSecurityName} (old)";
            }

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

        private static IEnumerable<QuickenCsvRow> ConvertOrders(IEnumerable<Order> orders, Dictionary<string, string> securityNameByIsin)
        {
            return orders.Select(order => new QuickenCsvRow
                                          {
                                              Action = order.Type == OrderType.Buy ? "Bought" : "Sold",
                                              Date = order.Date.ToString(DateFormats.Standard),
                                              Account = $"VIAC 3a ({order.PortfolioNumber})",
                                              Security = securityNameByIsin[order.Isin],
                                              OptionalSymbol = order.Isin,
                                              Shares = order.Units,
                                              Price = order.Price,
                                              Amount = order.Amount,
                                              Memo = order.Remark,
                                          });
        }

        private static IEnumerable<QuickenCsvRow> ConvertDividends(IEnumerable<Dividend> dividends, Dictionary<string, string> securityNameByIsin)
        {
            return dividends.Select(dividend => new QuickenCsvRow
                                                {
                                                    Action = "Div",
                                                    Date = dividend.Date.ToString(DateFormats.Standard),
                                                    Account = $"VIAC 3a ({dividend.PortfolioNumber})",
                                                    Security = securityNameByIsin[dividend.Isin],
                                                    OptionalSymbol = dividend.Isin,
                                                    Shares = dividend.Units,
                                                    Price = dividend.Payment,
                                                    Amount = dividend.Amount,
                                                    Memo = dividend.Remark,
                                                });
        }

        private static IEnumerable<QuickenCsvRow> ConvertDeposits(IEnumerable<Deposit> deposits)
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

        private static IEnumerable<QuickenCsvRow> ConvertInterests(IEnumerable<Interest> interests)
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

        private static IEnumerable<QuickenCsvRow> ConvertCommissions(IEnumerable<Commission> commissions)
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

        private static IEnumerable<QuickenCsvRow> ConvertMergers(List<Merger> mergers, List<Order> orders, Dictionary<string, string> securityNameByIsin)
        {
            foreach (var merger in mergers)
            {
                var mergerDate = merger.Date.ToString(DateFormats.Standard);
                var account = $"VIAC 3a ({merger.PortfolioNumber})";
                var sharesToRemove = orders.Where(order => order.Isin == merger.OldIsin && order.Type == OrderType.Buy).Sum(order => order.Units) -
                                     orders.Where(order => order.Isin == merger.OldIsin && order.Type == OrderType.Sell).Sum(order => order.Units);
                yield return new QuickenCsvRow
                             {
                                 Action = "Removed",
                                 Date = mergerDate,
                                 Account = account,
                                 Security = securityNameByIsin[merger.OldIsin],
                                 OptionalSymbol = merger.OldIsin,
                                 Shares = sharesToRemove,
                             };

                var sharesToAdd = sharesToRemove * merger.ConversionRatio;
                yield return new QuickenCsvRow
                             {
                                 Action = "Added",
                                 Date = mergerDate,
                                 Account = account,
                                 Security = securityNameByIsin[merger.NewIsin],
                                 OptionalSymbol = merger.NewIsin,
                                 Shares = sharesToAdd,
                             };
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
    }
}