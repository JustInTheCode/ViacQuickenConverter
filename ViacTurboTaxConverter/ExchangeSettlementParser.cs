namespace ViacTurboTaxConverter
{
    public class ExchangeSettlementParser
    {
        private readonly ExchangeRateClient _exchangeRateClient;

        public ExchangeSettlementParser(ExchangeRateClient exchangeRateClient)
        {
            _exchangeRateClient = exchangeRateClient;
        }

        public async Task<Order> ParseAsync(string text, string filePath)
        {
            var orderFields = ExtractOrderDetails(text, filePath);
            var units = orderFields.Amount / orderFields.Price;
            if (orderFields.Currency == "USD")
            {
                return new Order(orderFields.SecurityName,
                                 orderFields.OrderType,
                                 units,
                                 orderFields.Price,
                                 orderFields.Amount,
                                 orderFields.OrderDate,
                                 filePath);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(orderFields.Currency, "USD", orderFields.OrderDate);
            var usdPrice = orderFields.Price * exchangeRate;
            var usdAmount = orderFields.Amount * exchangeRate;
            var remark = $"Converted from {orderFields.Currency} to USD on {orderFields.OrderDate:yyyy-MM-dd}. " +
                         $"Exchange rate: {exchangeRate:F6}. Original price: {orderFields.Price:F2}, amount: {orderFields.Amount:F2}.";

            return new Order(orderFields.SecurityName,
                             orderFields.OrderType,
                             units,
                             usdPrice,
                             usdAmount,
                             orderFields.OrderDate,
                             filePath,
                             remark);
        }

        private static OrderFields ExtractOrderDetails(string text, string filePath)
        {
            var orderType = OrderType.Unknown;
            string? securityName = null;
            decimal price = 0;
            string? currency = null;
            decimal amount = 0;
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

            if (orderType == OrderType.Unknown)
            {
                throw new ValueNotFoundException("order type", filePath);
            }

            if (securityName is null)
            {
                throw new ValueNotFoundException("security name", filePath);
            }

            if (price == 0)
            {
                throw new ValueNotFoundException("price", filePath);
            }

            if (currency is null)
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

            return new OrderFields(securityName,
                                   orderType,
                                   price,
                                   amount,
                                   currency,
                                   orderDate);
        }

        private static OrderType GetOrderType(string line)
        {
            var orderLineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Exact);
            return Enum.TryParse(orderLineComponents[1], out OrderType orderType) ? orderType : throw new ValueInvalidException("order type", line);
        }

        private static string GetSecurityName(string line)
        {
            var unitsLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Minimum);
            var name = string.Join(" ", unitsLineComponents.Skip(2));
            return name.Length == 0 ? throw new ValueInvalidException(nameof(name), line) : name;
        }

        private static (decimal Price, string Currency) GetPriceAndCurrency(string line)
        {
            var priceLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            var currency = priceLineComponents[1];
            var cleanedPrice = priceLineComponents[2].Replace("'", "");
            return decimal.TryParse(cleanedPrice, out var price) ? (price, currency) : throw new ValueInvalidException(nameof(price), line);
        }

        private static decimal GetAmount(string line)
        {
            var amountLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            var cleanedAmount = amountLineComponents[2].Replace("'", "");
            return decimal.TryParse(cleanedAmount, out var amount) ? amount : throw new ValueInvalidException(nameof(amount), line);
        }

        private static DateTime GetOrderDate(string line)
        {
            var dateLineComponents = LineParser.SplitLine(line, 7, LineParser.WordCountRequirement.Exact);
            return DateTime.TryParse(dateLineComponents[4], out var orderDate) ? orderDate : throw new ValueInvalidException(nameof(orderDate), line);
        }

        private readonly record struct OrderFields(
            string SecurityName,
            OrderType OrderType,
            decimal Price,
            decimal Amount,
            string Currency,
            DateTime OrderDate);
    }

    public readonly record struct Order(
        string SecurityName,
        OrderType OrderType,
        decimal Units,
        decimal Price,
        decimal Amount,
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