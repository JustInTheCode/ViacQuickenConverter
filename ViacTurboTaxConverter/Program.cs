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

        public static async Task Main(string[] _)
        {
            try
            {
                using var exchangeRateClient = new ExchangeRateClient();
                var exchangeSettlementParser = new ExchangeSettlementParser(exchangeRateClient);
                var dividendPaymentParser = new DividendPaymentParser(exchangeRateClient);
                var depositParser = new DepositParser(exchangeRateClient);
                var files = Directory.GetFiles(@"C:\Users\JustinThiede\Downloads\viac_all");
                List<Order> orders = [];
                List<Dividend> dividends = [];
                List<Deposit> deposits = [];
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

                Console.WriteLine($"Parsed {orders.Count} {ExchangeSettlement} statements.");
                Console.WriteLine($"Parsed {dividends.Count} {DividendPayment} statements.");
                Console.WriteLine($"Parsed {deposits.Count} {Deposit} statements.");
                Console.WriteLine($"Parsed {orders.Count + dividends.Count + deposits.Count} files.");
            }
            catch (Exception exception)
            {
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