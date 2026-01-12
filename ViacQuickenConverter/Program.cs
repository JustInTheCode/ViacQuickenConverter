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

        private const string DividendPaymentCancellation = "Cancelation Dividend Payment";

        private const string DividendPayment = "Dividend Payment";

        private const string TaxRefund = "Refund withholding tax";

        private const string Deposit = "Deposit 3a";

        private const string Interest = "Interest";

        private const string Commission = "Commission";

        private const string Merger = "Exchange Settlement Fund Fusion";

        private const string Reimbursement = "Reimbursement";

        public static async Task Main(string[] _)
        {
            try
            {
                var directoryPath = GetDirectoryPath();
                var filePaths = Directory.GetFiles(directoryPath, "*.pdf");
                if (filePaths.Length == 0)
                {
                    Console.WriteLine("Directory contains no files. Nothing to do.");
                    return;
                }

                using var exchangeRateClient = new ExchangeRateClient();
                var exchangeSettlementParser = new ExchangeSettlementParser(exchangeRateClient);
                var dividendCancellationParser = new DividendCancellationParser(exchangeRateClient);
                var dividendPaymentParser = new DividendPaymentParser(exchangeRateClient);
                var depositParser = new DepositParser(exchangeRateClient);
                var interestParser = new InterestParser(exchangeRateClient);
                var commissionParser = new CommissionParser(exchangeRateClient);
                var reimbursementParser = new ReimbursementParser(exchangeRateClient);
                List<Merger> mergers = [];
                List<Order> orders = [];
                List<DividendCancellation> dividendCancellations = [];
                List<Dividend> dividends = [];
                List<Deposit> deposits = [];
                List<Interest> interests = [];
                List<Commission> commissions = [];
                List<Reimbursement> reimbursements = [];
                foreach (var filePath in filePaths)
                {
                    using var pdf = PdfDocument.Open(filePath);
                    var pages = pdf.GetPages().ToArray();
                    if (pages.Length != 1)
                    {
                        throw new InvalidPageCountException(filePath, pages.Length);
                    }

                    var text = ContentOrderTextExtractor.GetText(pages[0]);
                    if (text.Contains(Merger))
                    {
                        mergers.Add(ParseWithLogging(Merger, filePath, () => MergerParser.Parse(text, filePath)));
                    }
                    else if (text.Contains(ExchangeSettlement))
                    {
                        orders.Add(await ParseWithLoggingAsync(ExchangeSettlement, filePath, () => exchangeSettlementParser.ParseAsync(text, filePath)));
                    }
                    else if (text.Contains(DividendPaymentCancellation))
                    {
                        dividendCancellations.Add(await ParseWithLoggingAsync(DividendPaymentCancellation, filePath, () => dividendCancellationParser.ParseAsync(text, filePath)));
                    }
                    else if (text.Contains(DividendPayment) || text.Contains(TaxRefund))
                    {
                        dividends.Add(await ParseWithLoggingAsync(DividendPayment, filePath, () => dividendPaymentParser.ParseAsync(text, filePath)));
                    }
                    else if (text.Contains(Deposit))
                    {
                        deposits.Add(await ParseWithLoggingAsync(Deposit, filePath, () => depositParser.ParseAsync(text, filePath)));
                    }
                    else if (text.Contains(Interest))
                    {
                        interests.Add(await ParseWithLoggingAsync(Interest, filePath, () => interestParser.ParseAsync(text, filePath)));
                    }
                    else if (text.Contains(Commission))
                    {
                        commissions.Add(await ParseWithLoggingAsync(Commission, filePath, () => commissionParser.ParseAsync(text, filePath)));
                    }
                    else if (text.Contains(Reimbursement))
                    {
                        reimbursements.Add(await ParseWithLoggingAsync(Reimbursement, filePath, () => reimbursementParser.ParseAsync(text, filePath)));
                    }
                    else
                    {
                        Console.WriteLine($"Skipping file '{filePath}' — unrecognized statement type.");
                    }
                }

                const int labelWidth = 34;
                var totalCount = orders.Count + dividendCancellations.Count + dividends.Count + deposits.Count + interests.Count + commissions.Count + mergers.Count +
                                 reimbursements.Count;
                Console.WriteLine($"{Environment.NewLine}Parsed Viac Statements:");
                Console.WriteLine($"  {$"{ExchangeSettlement}s:",-labelWidth} {orders.Count}");
                Console.WriteLine($"  {$"{DividendPaymentCancellation}s:",-labelWidth} {dividendCancellations.Count}");
                Console.WriteLine($"  {$"{DividendPayment}s:",-labelWidth} {dividends.Count}");
                Console.WriteLine($"  {$"{Deposit}s:",-labelWidth} {deposits.Count}");
                Console.WriteLine($"  {$"{Interest}s:",-labelWidth} {interests.Count}");
                Console.WriteLine($"  {$"{Commission}s:",-labelWidth} {commissions.Count}");
                Console.WriteLine($"  {$"{Merger}s:",-labelWidth} {mergers.Count}");
                Console.WriteLine($"  {$"{Reimbursement}s:",-labelWidth} {reimbursements.Count}");
                Console.WriteLine($"  {"Total",-labelWidth} {totalCount}");

                if (mergers.Count > 0)
                {
                    PromptMergerAcknowledgment();
                }

                Console.WriteLine($"{Environment.NewLine}Generating Quicken CSV file...");
                QuickenCsvWriter.Write(orders,
                                       dividendCancellations,
                                       dividends,
                                       deposits,
                                       interests,
                                       commissions,
                                       mergers);

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

        private static async Task<T> ParseWithLoggingAsync<T>(string type, string filePath, Func<Task<T>> parseFunc)
        {
            Console.WriteLine($"{Environment.NewLine}Parsing {type}, file: '{filePath}'.");
            var result = await parseFunc();
            Console.WriteLine($"Parsed {type}, file: '{filePath}'.");

            return result;
        }

        private static T ParseWithLogging<T>(string type, string filePath, Func<T> parseFunc)
        {
            Console.WriteLine($"{Environment.NewLine}Parsing {type}, file: '{filePath}'.");
            var result = parseFunc();
            Console.WriteLine($"Parsed {type}, file: '{filePath}'.");

            return result;
        }

        private static void PromptMergerAcknowledgment()
        {
            Console.WriteLine($"""
                               {Environment.NewLine}WARNING: Merger detected!
                               Mergers require special handling to maintain correct tax lots in Quicken.
                               You must either reimport ALL statements since account inception OR handle the merger manually.
                               See documentation for detailed instructions.
                               """);

            while (true)
            {
                Console.WriteLine($"{Environment.NewLine}Do you acknowledge this and wish to continue? (y/n): ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                switch (response)
                {
                    case "y":
                        return;

                    case "n":
                        Console.WriteLine($"{Environment.NewLine}Operation cancelled. No file was generated.");
                        Console.WriteLine("Press any key to exit.");
                        Console.ReadKey(true);
                        Environment.Exit(0);
                        return;

                    default:
                        Console.WriteLine("Incorrect input. Please enter 'y' or 'n'.");
                        break;
                }
            }
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