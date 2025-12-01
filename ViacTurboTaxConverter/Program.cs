using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ViacTurboTaxConverter
{
    internal class Program
    {
        public static async Task Main(string[] _)
        {
            const string exchangeSettlement = "Exchange Settlement";
            using var exchangeRateClient = new ExchangeRateClient();
            var exchangeSettlementParser = new ExchangeSettlementParser(exchangeRateClient);
            try
            {
                var files = Directory.GetFiles(@"C:\Users\JustinThiede\Downloads\viac_all");
                List<Order> orders = [];
                foreach (var file in files)
                {
                    using var pdf = PdfDocument.Open(file);
                    var pages = pdf.GetPages().ToArray();
                    if (pages.Length != 1)
                    {
                        throw new InvalidPageCountException(file, pages.Length);
                    }

                    var text = ContentOrderTextExtractor.GetText(pages[0]);
                    if (text.Contains(exchangeSettlement))
                    {
                        Console.WriteLine($"Parsing {exchangeSettlement}, file: '{file}'.");
                        orders.Add(await exchangeSettlementParser.ParseAsync(text, file));
                        Console.WriteLine($"Parsed {exchangeSettlement}, file: '{file}'.");
                    }
                }

                foreach (var order in orders)
                {
                    Console.WriteLine(order);
                }

                Console.WriteLine($"Parsed {orders.Count} {exchangeSettlement}s.");
                Console.WriteLine($"Parsed {files.Length} files.");
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