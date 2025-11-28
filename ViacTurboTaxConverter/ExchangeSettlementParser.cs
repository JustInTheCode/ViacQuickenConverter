namespace ViacTurboTaxConverter
{
    public class ExchangeSettlementParser
    {
        private readonly ExchangeRateRetriever _exchangeRateRetriever;

        public ExchangeSettlementParser(ExchangeRateRetriever exchangeRateRetriever)
        {
            _exchangeRateRetriever = exchangeRateRetriever;
        }

        public async Task<Order> ParseAsync(string text, string filePath)
        {
            var (orderType, securityName, price, currency, amount, orderDate) = GetValues(text);
            ValidateValues(filePath,
                           orderType,
                           securityName,
                           price,
                           currency,
                           amount,
                           orderDate);
            var units = amount / price;
            if (currency == "USD")
            {
                return new Order(securityName!,
                                 orderType,
                                 units,
                                 price,
                                 amount,
                                 currency,
                                 orderDate,
                                 filePath);
            }

            var exchangeRate = await _exchangeRateRetriever.GetExchangeRateAsync(currency!, "USD", orderDate);
            var remark = $"Converted the {currency} price {price} and amount {amount} to USD using the exchange rate {exchangeRate} from {orderDate:dd-MM-yyyy}.";
            amount *= exchangeRate;
            price *= exchangeRate;

            return new Order(securityName!,
                             orderType,
                             units,
                             price,
                             amount,
                             currency!,
                             orderDate,
                             filePath,
                             remark);
        }

        private static (OrderType orderType, string? securityName, double price, string? currency, double amount, DateTime orderDate) GetValues(string text)
        {
            var orderType = OrderType.Unknown;
            string? securityName = null;
            double price = 0;
            string? currency = null;
            double amount = 0;
            DateTime orderDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.StartsWith("Order:"))
                {
                    orderType = GetOrderType(line);
                }

                if (line.Contains("units") || line.Contains("Qty"))
                {
                    securityName = GetSecurityName(line);
                }

                if (line.Contains("Price:"))
                {
                    (price, currency) = GetPriceAndCurrency(line);
                }

                if (line.Contains("Amount"))
                {
                    amount = GetAmount(line);
                }

                if (line.Contains("Charged amount:"))
                {
                    orderDate = GetOrderDate(line);
                }
            }

            return (orderType, securityName, price, currency, amount, orderDate);
        }

        private static OrderType GetOrderType(string line)
        {
            var orderLineComponents = LineTokenizer.GetWords(line, 2, LineTokenizer.WordCountRequirement.Exact);
            return Enum.TryParse(orderLineComponents[1], out OrderType orderType) ? orderType : throw new ValueInvalidException(line, "order type");
        }

        private static string GetSecurityName(string line)
        {
            var unitsLineComponents = LineTokenizer.GetWords(line, 3, LineTokenizer.WordCountRequirement.Minimum);
            var name = string.Join(" ", unitsLineComponents.Skip(2));
            return name.Length == 0 ? throw new ValueInvalidException(line, nameof(name)) : name;
        }

        private static (double Price, string Currency) GetPriceAndCurrency(string line)
        {
            var priceLineComponents = LineTokenizer.GetWords(line, 3, LineTokenizer.WordCountRequirement.Exact);
            var currency = priceLineComponents[1];
            var cleanedPrice = priceLineComponents[2].Replace("'", "");
            return double.TryParse(cleanedPrice, out var price) ? (price, currency) : throw new ValueInvalidException(line, nameof(price));
        }

        private static double GetAmount(string line)
        {
            var amountLineComponents = LineTokenizer.GetWords(line, 3, LineTokenizer.WordCountRequirement.Exact);
            var cleanedAmount = amountLineComponents[2].Replace("'", "");
            return double.TryParse(cleanedAmount, out var amount) ? amount : throw new ValueInvalidException(line, nameof(amount));
        }

        private static DateTime GetOrderDate(string line)
        {
            var dateLineComponents = LineTokenizer.GetWords(line, 7, LineTokenizer.WordCountRequirement.Exact);
            return DateTime.TryParse(dateLineComponents[4], out var orderDate) ? orderDate : throw new ValueInvalidException(line, nameof(orderDate));
        }

        private static void ValidateValues(string filePath,
                                           OrderType orderType,
                                           string? securityName,
                                           double price,
                                           string? currency,
                                           double amount,
                                           DateTime orderDate)
        {
            if (orderType == OrderType.Unknown)
            {
                throw new ValueNotFoundException("order type", filePath);
            }

            if (securityName == null)
            {
                throw new ValueNotFoundException("security name", filePath);
            }

            if (price == 0)
            {
                throw new ValueNotFoundException("price", filePath);
            }

            if (currency == null)
            {
                throw new ValueNotFoundException("currency", filePath);
            }

            if (amount == 0)
            {
                throw new ValueNotFoundException("amount", filePath);
            }

            if (orderDate == default)
            {
                throw new ValueNotFoundException("order date", filePath);
            }
        }
    }

    public readonly record struct Order(
        string SecurityName,
        OrderType OrderType,
        double Units,
        double Price,
        double Amount,
        string OriginalCurrency,
        DateTime OrderDate,
        string FilePath,
        string? Remark = null);

    public enum OrderType
    {
        Unknown,

        Buy,

        Sell,
    }
}