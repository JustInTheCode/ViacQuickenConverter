using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ViacTurboTaxConverter
{
    internal class Program
    {
        private const string ExchangeSettlement = "Exchange Settlement";

        private const string DividendPayment = "Dividend Payment";

        private const string TaxRefund = "Refund withholding tax";

        private const string Deposit = "Deposit 3a";

        private const string Interest = "Interest";

        private const string Commission = "Commission";

        public static async Task Main(string[] _)
        {
            try
            {
                using var exchangeRateClient = new ExchangeRateClient();
                var exchangeSettlementParser = new ExchangeSettlementParser(exchangeRateClient);
                var dividendPaymentParser = new DividendPaymentParser(exchangeRateClient);
                var depositParser = new DepositParser(exchangeRateClient);
                var interestParser = new InterestParser(exchangeRateClient);
                var commissionParser = new CommissionParser(exchangeRateClient);
                var files = Directory.GetFiles(@"C:\Users\JustinThiede\Downloads\viac_all");
                List<Order> orders = [];
                List<Dividend> dividends = [];
                List<Deposit> deposits = [];
                List<Interest> interests = [];
                List<Commission> commissions = [];
                foreach (var file in files)
                {
                    using var pdf = PdfDocument.Open(file);
                    var pages = pdf.GetPages().ToArray();
                    if (pages.Length != 1)
                    {
                        throw new InvalidPageCountException(file, pages.Length);
                    }

                    var text = ContentOrderTextExtractor.GetText(pages[0]);
                    if (text.Contains(ExchangeSettlement))
                    {
                        Console.WriteLine($"Parsing {ExchangeSettlement}, file: '{file}'.");
                        orders.Add(await exchangeSettlementParser.ParseAsync(text, file));
                        Console.WriteLine($"Parsed {ExchangeSettlement}, file: '{file}'.");
                    }
                    else if (text.Contains(DividendPayment) || text.Contains(TaxRefund))
                    {
                        Console.WriteLine($"Parsing {DividendPayment}, file: '{file}'.");
                        dividends.Add(await dividendPaymentParser.ParseAsync(text, file));
                        Console.WriteLine($"Parsed {DividendPayment}, file: '{file}'.");
                    }
                    else if (text.Contains(Deposit))
                    {
                        Console.WriteLine($"Parsing {Deposit}, file: '{file}'.");
                        deposits.Add(await depositParser.ParseAsync(text, file));
                        Console.WriteLine($"Parsed {Deposit}, file: '{file}'.");
                    }
                    else if (text.Contains(Interest))
                    {
                        Console.WriteLine($"Parsing {Interest}, file: '{file}'.");
                        interests.Add(await interestParser.ParseAsync(text, file));
                        Console.WriteLine($"Parsed {Interest}, file: '{file}'.");
                    }
                    else if (text.Contains(Commission))
                    {
                        Console.WriteLine($"Parsing {Commission}, file: '{file}'.");
                        commissions.Add(await commissionParser.ParseAsync(text, file));
                        Console.WriteLine($"Parsed {Commission}, file: '{file}'.");
                    }
                    else
                    {
                        Console.WriteLine($"Skipping unsupported file: '{file}'. File is not a recognized statement type. " +
                                          $"Supported types: {ExchangeSettlement}, {DividendPayment}, {TaxRefund}, {Deposit}, {Interest}, {Commission}.");
                    }
                }

                foreach (var order in orders)
                {
                    Console.WriteLine(order);
                }

                foreach (var dividend in dividends)
                {
                    Console.WriteLine(dividend);
                }

                foreach (var deposit in deposits)
                {
                    Console.WriteLine(deposit);
                }

                foreach (var interest in interests)
                {
                    Console.WriteLine(interest);
                }

                foreach (var commission in commissions)
                {
                    Console.WriteLine(commission);
                }

                Console.WriteLine($"Parsed {orders.Count} {ExchangeSettlement} statements.");
                Console.WriteLine($"Parsed {dividends.Count} {DividendPayment} statements.");
                Console.WriteLine($"Parsed {deposits.Count} {Deposit} statements.");
                Console.WriteLine($"Parsed {interests.Count} {Interest} statements.");
                Console.WriteLine($"Parsed {commissions.Count} {Commission} statements.");
                Console.WriteLine($"Parsed {orders.Count + dividends.Count + deposits.Count + interests.Count + commissions.Count} files.");
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

        private class InvalidPageCountException : Exception
        {
            public InvalidPageCountException(string filePath, int actualPageCount) :
                base($"PDF file `{filePath}` must contain exactly 1 page but contains {actualPageCount} page(s).")
            {
            }
        }
    }
}