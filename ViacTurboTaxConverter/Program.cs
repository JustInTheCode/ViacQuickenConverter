using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ViacTurboTaxConverter
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var files = Directory.GetFiles(@"C:\Users\JustinThiede\Downloads\documents");

            foreach (var file in files)
            {
                var order = new Order { Source = Path.GetFileName(file) };
                using var pdf = PdfDocument.Open(file);
                var pages = pdf.GetPages().ToArray();
                if (pages.Length != 1)
                {
                    Console.WriteLine($"The PDF '{file}' should contain exactly one page.");
                    return;
                }

                var text = ContentOrderTextExtractor.GetText(pages[0]);
                foreach (var line in text.Split(Environment.NewLine))
                {
                    if (line.StartsWith("Order:"))
                    {
                        if (!Enum.TryParse(line.Split(" ")[1], out OrderType orderType))
                        {
                            Console.WriteLine($"{line} does not contain a valid order type.");
                            return;
                        }

                        order.OrderType = orderType;
                    }

                    if (line.Contains("units"))
                    {
                        var unitsLineComponents = line.Split(" ");
                        if (unitsLineComponents.Length < 3)
                        {
                            Console.WriteLine($"{line} should contain at least 3 words.");
                            return;
                        }

                        if (!double.TryParse(unitsLineComponents[0], out var units))
                        {
                            Console.WriteLine($"{line} does not contain a valid units.");
                            return;
                        }

                        order.Units = units;
                        order.Name = string.Join(" ", unitsLineComponents.Skip(2));
                        if (order.Name.Length == 0)
                        {
                            Console.WriteLine($"{line} does not contain a valid name.");
                            return;
                        }
                    }

                    if (line.Contains("ISIN"))
                    {
                        var isinLineComponents = line.Split(" ");
                        if (isinLineComponents.Length != 2)
                        {
                            Console.WriteLine($"{line} should contain 2 words.");
                            return;
                        }

                        order.Isin = isinLineComponents[1];
                    }

                    if (line.Contains("Price:"))
                    {
                        var priceLineComponents = line.Split(" ");
                        if (priceLineComponents.Length != 3)
                        {
                            Console.WriteLine($"{line} should contain 3 words.");
                            return;
                        }

                        var cleanedPrice = priceLineComponents[2].Replace("'", "");
                        if (!double.TryParse(cleanedPrice, out var price))
                        {
                            Console.WriteLine($"{line} does not contain a valid price.");
                            return;
                        }

                        order.OriginalCurrency = priceLineComponents[1];
                        order.Price = price;
                    }

                    if (line.Contains("Amount"))
                    {
                        var amountLineComponents = line.Split(" ");
                        if (amountLineComponents.Length != 3)
                        {
                            Console.WriteLine($"{line} should contain 3 words.");
                            return;
                        }

                        var cleanedAmount = amountLineComponents[2].Replace("'", "");
                        if (!double.TryParse(cleanedAmount, out var amount))
                        {
                            Console.WriteLine($"{line} does not contain a valid amount.");
                            return;
                        }

                        order.Amount = amount;
                    }

                    if (line.Contains("Charged amount:"))
                    {
                        var dateLineComponents = line.Split(" ");
                        if (dateLineComponents.Length != 7)
                        {
                            Console.WriteLine($"{line} should contain 7 words.");
                            return;
                        }

                        if (!DateTime.TryParse(dateLineComponents[4], out var date))
                        {
                            Console.WriteLine($"{line} does not contain a valid date.");
                            return;
                        }

                        order.Date = date;
                    }
                }

                if (order.OriginalCurrency != "USD")
                {
                    var exchangeRate = await GetExchangeRateAsync(order.Date, order.OriginalCurrency, "USD");
                    order.Remark =
                        $"Converted the {order.OriginalCurrency} price {order.Price} and amount {order.Amount} to USD using the exchange rate {exchangeRate} from {order.Date:dd-MM-yyyy}.";
                    order.Amount *= exchangeRate;
                    order.Price *= exchangeRate;
                }

                Console.WriteLine(order);
            }
        }

        private static async Task<double> GetExchangeRateAsync(DateTime date, string fromCurrency, string toCurrency)
        {
            using var httpClient = new HttpClient();
            var dateStr = date.ToString("yyyy-MM-dd");
            var url = $"https://api.frankfurter.app/{dateStr}?from={fromCurrency}&to={toCurrency}";

            var response = await httpClient.GetStringAsync(url);
            var json = System.Text.Json.JsonDocument.Parse(response);

            return json.RootElement.GetProperty("rates").GetProperty(toCurrency).GetDouble();
        }

        private record struct Order(
            string Name,
            string Isin,
            OrderType OrderType,
            double Units, // todo: clarify if it's okay that units are rounded, if not calculate Amount / Price
            double Price, // todo: clarify if European Central Bank (ECB) reference rate are okay, small dif to mid market rate e.g. 08.01.2025 CAD to USD from ECB 0.69486 from Wise 0.6961
            double Amount,
            string OriginalCurrency,
            DateTime Date,
            string Source,
            string? Remark);

        private enum OrderType
        {
            Buy,

            Sell,
        }
    }
}