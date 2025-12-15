using System;
using System.Linq;
using System.Threading.Tasks;
using ViacQuickenConverter.Viac.CurrencyConversion;
using ViacQuickenConverter.Viac.Error;
using ViacQuickenConverter.Viac.Text;

namespace ViacQuickenConverter.Viac
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
            if (orderFields.Currency == "USD")
            {
                return new Order(orderFields.PortfolioNumber,
                                 orderFields.SecurityName,
                                 orderFields.Isin,
                                 orderFields.Type,
                                 units,
                                 orderFields.Price,
                                 orderFields.Amount,
                                 orderFields.Date);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(orderFields.Currency, "USD", orderFields.Date);
            var usdPrice = orderFields.Price * exchangeRate;
            var usdAmount = orderFields.Amount * exchangeRate;
            var remark = $"{orderFields.Currency}-USD ({exchangeRate:F6}), Price: {orderFields.Price:F2}, Amt: {orderFields.Amount:F2}";

            return new Order(orderFields.PortfolioNumber,
                             orderFields.SecurityName,
                             orderFields.Isin,
                             orderFields.Type,
                             units,
                             usdPrice,
                             usdAmount,
                             orderFields.Date,
                             remark);
        }

        private static OrderFields ExtractOrderDetails(string text, string filePath)
        {
            string? portfolioNumber = null;
            var orderType = OrderType.Unknown;
            string? securityName = null;
            string? isin = null;
            decimal price = 0;
            string? currency = null;
            decimal amount = 0;
            DateTime orderDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.StartsWith("Portfolio"))
                {
                    portfolioNumber = GetPortfolioNumber(line);
                }
                else if (line.StartsWith("Order:"))
                {
                    orderType = GetOrderType(line);
                }
                else if (line.Contains("units") || line.Contains("Qty"))
                {
                    securityName = GetSecurityName(line);
                }
                else if (line.Contains("ISIN:"))
                {
                    isin = GetIsin(line);
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

            if (portfolioNumber == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.PortfolioNumber, filePath);
            }

            if (orderType == OrderType.Unknown)
            {
                throw new ValueNotFoundException(ErrorFieldNames.OrderType, filePath);
            }

            if (securityName is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.SecurityName, filePath);
            }

            if (isin is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Isin, filePath);
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

            return new OrderFields(portfolioNumber,
                                   securityName,
                                   isin,
                                   orderType,
                                   price,
                                   amount,
                                   currency,
                                   orderDate);
        }

        private static string GetPortfolioNumber(string line)
        {
            const int expectedWordNumber = 2;
            var lineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Exact);
            return lineComponents[expectedWordNumber - 1];
        }

        private static OrderType GetOrderType(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 2;
            var orderTypeString = lineComponents[expectedWordNumber - 1];

            return Enum.TryParse(orderTypeString, out OrderType orderType) ? orderType :
                       throw new ValueInvalidException(ErrorFieldNames.OrderType, line, expectedWordNumber, orderTypeString);
        }

        private static string GetSecurityName(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Minimum);
            var name = string.Join(" ", lineComponents.Skip(2));
            if (name.Length == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.SecurityName, line);
            }

            name = name.Replace("(old)", string.Empty);
            name = MultipleWhitespaceRegex().Replace(name, " ");

            return name.Trim();
        }

        private static string GetIsin(string line)
        {
            const int expectedWordNumber = 2;
            var lineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Exact);
            return lineComponents[expectedWordNumber - 1];
        }

        private static (decimal Price, string Currency) GetPriceAndCurrency(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            const int expectedPriceWordNumber = 3;
            var priceString = lineComponents[expectedPriceWordNumber - 1].Replace("'", "");
            var currency = lineComponents[1];

            return decimal.TryParse(priceString, out var price) ? (price, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.Price, line, expectedPriceWordNumber, priceString);
        }

        private static decimal GetAmount(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 3;
            var amountString = lineComponents[expectedWordNumber - 1].Replace("'", "");

            return decimal.TryParse(amountString, out var amount) ? amount : throw new ValueInvalidException(ErrorFieldNames.Amount, line, expectedWordNumber, amountString);
        }

        private static DateTime GetOrderDate(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 7, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 5;
            var orderDateString = lineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(orderDateString, out var orderDate) ? orderDate :
                       throw new ValueInvalidException(ErrorFieldNames.OrderDate, line, expectedWordNumber, orderDateString);
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
        private static partial System.Text.RegularExpressions.Regex MultipleWhitespaceRegex();

        private readonly record struct OrderFields(
            string PortfolioNumber,
            string SecurityName,
            string Isin,
            OrderType Type,
            decimal Price,
            decimal Amount,
            string Currency,
            DateTime Date);
    }

    /// <summary>
    ///     Represents a security purchase or sale order from a VIAC statement.
    /// </summary>
    /// <param name="PortfolioNumber">The portfolio number associated with the order.</param>
    /// <param name="SecurityName">The name of the security that was bought or sold.</param>
    /// <param name="Isin">The ISIN of the security that was bought or sold.</param>
    /// <param name="Type">The type of order (buy or sell).</param>
    /// <param name="Units">Units (amount ÷ price).</param>
    /// <param name="Price">Price per unit.</param>
    /// <param name="Amount">Total order amount (price × units).</param>
    /// <param name="Date">The date the order was executed.</param>
    /// <param name="Remark">Notes about the currency conversion.</param>
    public readonly record struct Order(
        string PortfolioNumber,
        string SecurityName,
        string Isin,
        OrderType Type,
        decimal Units,
        decimal Price,
        decimal Amount,
        DateTime Date,
        string Remark = "Already in USD, no currency conversion necessary");

    public enum OrderType
    {
        Unknown,

        Buy,

        Sell,
    }
}