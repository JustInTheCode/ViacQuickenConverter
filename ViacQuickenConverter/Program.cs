using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using ViacQuickenConverter.Quicken;
using ViacQuickenConverter.Viac;
using ViacQuickenConverter.Viac.CurrencyConversion;

namespace ViacQuickenConverter
{
    internal static class Program
    {
        private const string ExchangeSettlement = "Exchange Settlement";

        private const string DividendPayment = "Dividend Payment";

        private const string TaxRefund = "Refund withholding tax";

        private const string Deposit = "Deposit 3a";

        private const string Interest = "Interest";

        private const string Commission = "Commission";

        private const string Merger = "Exchange Settlement Fund Fusion";

        public static async Task Main(string[] _)
        {
            try
            {
                var directoryPath = GetDirectoryPath();
                var files = Directory.GetFiles(directoryPath, "*.pdf");
                if (files.Length == 0)
                {
                    Console.WriteLine("Directory contains no files. Nothing to do.");
                    return;
                }

                using var exchangeRateClient = new ExchangeRateClient();
                var exchangeSettlementParser = new ExchangeSettlementParser(exchangeRateClient);
                var dividendPaymentParser = new DividendPaymentParser(exchangeRateClient);
                var depositParser = new DepositParser(exchangeRateClient);
                var interestParser = new InterestParser(exchangeRateClient);
                var commissionParser = new CommissionParser(exchangeRateClient);
                List<Commission> commissions = [];
                List<Deposit> deposits = [];
                List<Dividend> dividends = [];
                List<Interest> interests = [];
                List<Order> orders = [];
                List<Merger> mergers = [];
                foreach (var file in files)
                {
                    using var pdf = PdfDocument.Open(file);
                    var pages = pdf.GetPages().ToArray();
                    if (pages.Length != 1)
                    {
                        throw new InvalidPageCountException(file, pages.Length);
                    }

                    var text = ContentOrderTextExtractor.GetText(pages[0]);
                    if (text.Contains(Merger))
                    {
                        mergers.Add(ParseWithLogging(Merger, file, () => MergerParser.Parse(text, file)));
                    }
                    else if (text.Contains(ExchangeSettlement))
                    {
                        orders.Add(await ParseWithLoggingAsync(ExchangeSettlement, file, () => exchangeSettlementParser.ParseAsync(text, file)));
                    }
                    else if (text.Contains(DividendPayment) || text.Contains(TaxRefund))
                    {
                        dividends.Add(await ParseWithLoggingAsync(DividendPayment, file, () => dividendPaymentParser.ParseAsync(text, file)));
                    }
                    else if (text.Contains(Deposit))
                    {
                        deposits.Add(await ParseWithLoggingAsync(Deposit, file, () => depositParser.ParseAsync(text, file)));
                    }
                    else if (text.Contains(Interest))
                    {
                        interests.Add(await ParseWithLoggingAsync(Interest, file, () => interestParser.ParseAsync(text, file)));
                    }
                    else if (text.Contains(Commission))
                    {
                        commissions.Add(await ParseWithLoggingAsync(Commission, file, () => commissionParser.ParseAsync(text, file)));
                    }
                    else
                    {
                        Console.WriteLine($"Skipping file '{file}' — unrecognized statement type.");
                    }
                }

                const int labelWidth = 22;
                Console.WriteLine($"{Environment.NewLine}Parsed Viac Statements:");
                Console.WriteLine($"  {"Exchange Settlements:",-labelWidth} {orders.Count}");
                Console.WriteLine($"  {"Dividend Payments:",-labelWidth} {dividends.Count}");
                Console.WriteLine($"  {"Deposits:",-labelWidth} {deposits.Count}");
                Console.WriteLine($"  {"Interests:",-labelWidth} {interests.Count}");
                Console.WriteLine($"  {"Commissions:",-labelWidth} {commissions.Count}");
                Console.WriteLine($"  {"Mergers:",-labelWidth} {mergers.Count}");
                Console.WriteLine($"  {"Total",-labelWidth} {orders.Count + dividends.Count + deposits.Count + interests.Count + commissions.Count + mergers.Count}");

                Console.WriteLine($"{Environment.NewLine}Generating Quicken CSV file...");
                QuickenCsvWriter.Write(orders, dividends, deposits, interests, commissions);

                Console.WriteLine($"{Environment.NewLine}Done. Press any key to exit.");
                Console.ReadKey(true);
            }
            catch (Exception exception)
            {
                if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development")
                {
                    throw;
                }

                Console.WriteLine(exception.Message);
            }
        }

        private static string GetDirectoryPath()
        {
            while (true)
            {
                Console.WriteLine("Enter directory path to read Viac statements from: ");
                var directoryPath = Console.ReadLine();
                if (Directory.Exists(directoryPath))
                {
                    return directoryPath;
                }

                Console.WriteLine("Directory doesn't exist. Please try again.");
            }
        }

        private static async Task<T> ParseWithLoggingAsync<T>(string type, string file, Func<Task<T>> parseFunc)
        {
            Console.WriteLine($"{Environment.NewLine}Parsing {type}, file: '{file}'.");
            var result = await parseFunc();
            Console.WriteLine($"Parsed {type}, file: '{file}'.");

            return result;
        }

        private static T ParseWithLogging<T>(string type, string file, Func<T> parseFunc)
        {
            Console.WriteLine($"{Environment.NewLine}Parsing {type}, file: '{file}'.");
            var result = parseFunc();
            Console.WriteLine($"Parsed {type}, file: '{file}'.");

            return result;
        }

        private class InvalidPageCountException : Exception
        {
            public InvalidPageCountException(string filePath, int actualPageCount) :
                base($"PDF file `{filePath}` must contain exactly 1 page but contains {actualPageCount} page(s).")
            {
            }
        }
    }
}