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
            var filePath = GetUniqueFilePath(AppContext.BaseDirectory, $"viac_quicken_{DateTime.Now.ToString(DateFormats.Standard)}", ".csv");
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            var rows = new List<QuickenCsvRow>();
            rows.AddRange(ConvertOrders(orders));
            rows.AddRange(ConvertDividends(dividends));
            rows.AddRange(ConvertDeposits(deposits));
            rows.AddRange(ConvertInterests(interests));
            rows.AddRange(ConvertCommissions(commissions));
            rows.AddRange(ConvertMergers(mergers, orders));
            csv.WriteRecords(rows);

            Console.WriteLine($"Rows written: {rows.Count}");
            Console.WriteLine($"File saved to: {filePath}");
        }

        private static IEnumerable<QuickenCsvRow> ConvertOrders(IEnumerable<Order> orders)
        {
            return orders.Select(order => new QuickenCsvRow
                                          {
                                              Action = order.Type == OrderType.Buy ? "Bought" : "Sold",
                                              Date = order.Date.ToString(DateFormats.Standard),
                                              Account = $"VIAC 3a ({order.PortfolioNumber})",
                                              Security = order.SecurityName,
                                              OptionalSymbol = order.Isin,
                                              Shares = order.Units,
                                              Price = order.Price,
                                              Amount = order.Amount,
                                              Memo = order.Remark,
                                          });
        }

        private static IEnumerable<QuickenCsvRow> ConvertDividends(IEnumerable<Dividend> dividends)
        {
            return dividends.Select(dividend => new QuickenCsvRow
                                                {
                                                    Action = "Div",
                                                    Date = dividend.Date.ToString(DateFormats.Standard),
                                                    Account = $"VIAC 3a ({dividend.PortfolioNumber})",
                                                    Security = dividend.SecurityName,
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

        private static IEnumerable<QuickenCsvRow> ConvertMergers(List<Merger> mergers, List<Order> orders)
        {
            foreach (var merger in mergers)
            {
                var mergerDate = merger.Date.ToString(DateFormats.Standard);
                var account = $"VIAC 3a ({merger.PortfolioNumber})";
                var sharesToRemove = orders.Where(order => order.Isin == merger.OldIsin).Sum(order => order.Units);
                yield return new QuickenCsvRow
                             {
                                 Action = "Removed",
                                 Date = mergerDate,
                                 Account = account,
                                 Security = merger.OldSecurityName,
                                 OptionalSymbol = merger.OldIsin,
                                 Shares = sharesToRemove,
                             };

                var sharesToAdd = sharesToRemove * merger.ConversionRatio;
                yield return new QuickenCsvRow
                             {
                                 Action = "Added",
                                 Date = mergerDate,
                                 Account = account,
                                 Security = merger.NewSecurityName,
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