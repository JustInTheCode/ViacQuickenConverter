using ViacQuickenConverter.StatementParsing.CurrencyConversion;
using ViacQuickenConverter.StatementParsing.Error;
using ViacQuickenConverter.StatementParsing.Formatting;
using ViacQuickenConverter.StatementParsing.Text;

namespace ViacQuickenConverter.StatementParsing
{
    public partial class ExchangeSettlementParser
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
            var fileName = Path.GetFileName(filePath);
            if (orderFields.Currency == "USD")
            {
                return new Order(orderFields.SecurityName,
                                 orderFields.Type,
                                 units,
                                 orderFields.Price,
                                 orderFields.Amount,
                                 orderFields.Date,
                                 fileName);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(orderFields.Currency, "USD", orderFields.Date);
            var usdPrice = orderFields.Price * exchangeRate;
            var usdAmount = orderFields.Amount * exchangeRate;
            var remark = $"Converted from {orderFields.Currency} to USD on {orderFields.Date.ToString(DateFormats.Standard)} ({DateFormats.Standard}). " +
                         $"Exchange rate: {exchangeRate:F6}. Original share price: {orderFields.Price:F2}, total order amount: {orderFields.Amount:F2}.";

            return new Order(orderFields.SecurityName,
                             orderFields.Type,
                             units,
                             usdPrice,
                             usdAmount,
                             orderFields.Date,
                             fileName,
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
                else if (line.Contains("units") || line.Contains("Qty"))
                {
                    securityName = GetSecurityName(line);
                }
                else if (line.Contains("Price:"))
                {
                    (price, currency) = GetPriceAndCurrency(line);
                }
                else if (line.Contains("Amount"))
                {
                    amount = GetAmount(line);
                }
                else if (line.Contains("Charged amount:"))
                {
                    orderDate = GetOrderDate(line);
                }
            }

            if (orderType == OrderType.Unknown)
            {
                throw new ValueNotFoundException(ErrorFieldNames.OrderType, filePath);
            }

            if (securityName is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.SecurityName, filePath);
            }

            if (price == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Price, filePath);
            }

            if (currency is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Currency, filePath);
            }

            if (amount == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Amount, filePath);
            }

            if (orderDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.OrderDate, filePath);
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
            const int expectedWordNumber = 2;
            var orderTypeString = orderLineComponents[expectedWordNumber - 1];

            return Enum.TryParse(orderTypeString, out OrderType orderType) ? orderType :
                       throw new ValueInvalidException(ErrorFieldNames.OrderType, line, expectedWordNumber, orderTypeString);
        }

        private static string GetSecurityName(string line)
        {
            var unitsLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Minimum);
            var name = string.Join(" ", unitsLineComponents.Skip(2));
            if (name.Length == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.SecurityName, line);
            }

            name = name.Replace("(old)", string.Empty);
            name = MultipleWhitespaceRegex().Replace(name, " ");

            return name.Trim();
        }

        private static (decimal Price, string Currency) GetPriceAndCurrency(string line)
        {
            var priceLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            const int expectedPriceWordNumber = 3;
            var priceString = priceLineComponents[expectedPriceWordNumber - 1].Replace("'", "");
            var currency = priceLineComponents[1];

            return decimal.TryParse(priceString, out var price) ? (price, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.Price, line, expectedPriceWordNumber, priceString);
        }

        private static decimal GetAmount(string line)
        {
            var amountLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 3;
            var amountString = amountLineComponents[expectedWordNumber - 1].Replace("'", "");

            return decimal.TryParse(amountString, out var amount) ? amount : throw new ValueInvalidException(ErrorFieldNames.Amount, line, expectedWordNumber, amountString);
        }

        private static DateTime GetOrderDate(string line)
        {
            var dateLineComponents = LineParser.SplitLine(line, 7, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 5;
            var orderDateString = dateLineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(orderDateString, out var orderDate) ? orderDate :
                       throw new ValueInvalidException(ErrorFieldNames.OrderDate, line, expectedWordNumber, orderDateString);
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
        private static partial System.Text.RegularExpressions.Regex MultipleWhitespaceRegex();

        private readonly record struct OrderFields(
            string SecurityName,
            OrderType Type,
            decimal Price,
            decimal Amount,
            string Currency,
            DateTime Date);
    }

    /// <summary>
    ///     Represents a security purchase or sale order from a VIAC statement.
    /// </summary>
    /// <param name="SecurityName">The name of the security that was bought or sold.</param>
    /// <param name="Type">The type of order (buy or sell).</param>
    /// <param name="Units">Units (amount ÷ price).</param>
    /// <param name="Price">Price per unit.</param>
    /// <param name="Amount">Total order amount (price × units).</param>
    /// <param name="Date">The date the order was executed.</param>
    /// <param name="FileName">The file name of the source statement containing this order transaction.</param>
    /// <param name="Remark">Optional notes about the order, such as currency conversion details.</param>
    public readonly record struct Order(
        string SecurityName,
        OrderType Type,
        decimal Units,
        decimal Price,
        decimal Amount,
        DateTime Date,
        string FileName,
        string? Remark = null);

    public enum OrderType
    {
        Unknown,

        Buy,

        Sell,
    }
}