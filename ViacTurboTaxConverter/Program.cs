using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

internal class Program
{
    public static void Main(string[] args)
    {
        var currentDir = Directory.GetCurrentDirectory();

        var order = new Order();
        using var pdf = PdfDocument.Open(Path.Combine(currentDir, "test.pdf"));
        var pages = pdf.GetPages().ToArray();
        if (pages.Length != 1)
        {
            Console.WriteLine("The PDF should contain exactly one page.");
        }

        var text = ContentOrderTextExtractor.GetText(pages[0]);
        Console.WriteLine(text);
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

                var nameBuilder = new StringBuilder();
                for (var i = unitsLineComponents.Length - 1; i > 1; i--)
                {
                    nameBuilder.Append(unitsLineComponents[i]);
                    nameBuilder.Append(' ');
                }

                order.Name = nameBuilder.ToString();
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
                }

                if (priceLineComponents[1] != "USD")
                {
                    //todo: get exchange rate
                }

                if (!double.TryParse(priceLineComponents[2], out var price))
                {
                    Console.WriteLine($"{line} does not contain a valid price.");
                }

                order.Price = price;
            }

            if (line.Contains("Amount"))
            {
                var amountLineComponents = line.Split(" ");
                if (amountLineComponents.Length != 3)
                {
                    Console.WriteLine($"{line} should contain 3 words.");
                }

                if (amountLineComponents[1] != "USD")
                {
                    //todo: get exchange rate
                }

                if (!double.TryParse(amountLineComponents[2], out var amount))
                {
                    Console.WriteLine($"{line} does not contain a valid amount.");
                }

                order.Amount = amount;
            }

            if (line.Contains("Charged amount:"))
            {
                var dateLineComponents = line.Split(" ");
                if (dateLineComponents.Length != 7)
                {
                    Console.WriteLine($"{line} should contain 7 words.");
                }

                if (!DateTime.TryParse(dateLineComponents[4], out var date))
                {
                    Console.WriteLine($"{line} does not contain a valid date.");
                }

                order.Date = date;
            }
        }

        Console.WriteLine(order);
    }

    private record struct Order(
        string Name,
        string Isin,
        OrderType OrderType,
        double Units,
        double Price,
        double Amount,
        DateTime Date);

    private enum OrderType
    {
        Buy,

        Sell,
    }
}