using System;
using System.Linq;
using System.Threading.Tasks;
using ViacQuickenConverter.Viac.CurrencyConversion;
using ViacQuickenConverter.Viac.Error;
using ViacQuickenConverter.Viac.Text;

namespace ViacQuickenConverter.Viac
{
    public partial class DividendPaymentParser
    {
        private readonly ExchangeRateClient _exchangeRateClient;

        public DividendPaymentParser(ExchangeRateClient exchangeRateClient)
        {
            _exchangeRateClient = exchangeRateClient;
        }

        public async Task<Dividend> ParseAsync(string text, string filePath)
        {
            var dividendFields = ExtractDividendDetails(text, filePath);
            var units = dividendFields.Amount / dividendFields.Payment;
            if (dividendFields.Currency == "USD")
            {
                return new Dividend(dividendFields.PortfolioNumber,
                                    dividendFields.SecurityName,
                                    dividendFields.Isin,
                                    units,
                                    dividendFields.Payment,
                                    dividendFields.Amount,
                                    dividendFields.Date,
                                    $"Already in USD, no currency conversion necessary, Type: {dividendFields.Type}");
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(dividendFields.Currency, "USD", dividendFields.Date);
            var usdPayment = dividendFields.Payment * exchangeRate;
            var usdAmount = dividendFields.Amount * exchangeRate;
            var remark = $"{dividendFields.Currency}-USD ({exchangeRate:F6}), Amt: {dividendFields.Amount:F2}, Type: {dividendFields.Type}";

            return new Dividend(dividendFields.PortfolioNumber,
                                dividendFields.SecurityName,
                                dividendFields.Isin,
                                units,
                                usdPayment,
                                usdAmount,
                                dividendFields.Date,
                                remark);
        }

        private static DividendFields ExtractDividendDetails(string text, string filePath)
        {
            string? portfolioNumber = null;
            string? dividendType = null;
            string? securityName = null;
            string? isin = null;
            decimal payment = 0;
            string? currency = null;
            decimal amount = 0;
            DateTime dividendDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.StartsWith("Portfolio"))
                {
                    portfolioNumber = GetPortfolioNumber(line);
                }
                else if (line.StartsWith("Type of dividend:"))
                {
                    dividendType = GetDividendType(line);
                }
                else if (line.Contains("units") || line.Contains("Qty"))
                {
                    securityName = GetSecurityName(line);
                }
                else if (line.Contains("ISIN:"))
                {
                    isin = GetIsin(line);
                }
                else if (line.Contains("Dividend payment:"))
                {
                    (payment, currency) = GetPaymentAndCurrency(line);
                }
                else if (line.Contains("Amount") && !line.Contains("Amount credited:"))
                {
                    amount = GetAmount(line);
                }
                else if (line.Contains("Amount credited:"))
                {
                    dividendDate = GetDividendDate(line);
                }
            }

            if (portfolioNumber == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.PortfolioNumber, filePath);
            }

            if (dividendType == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.DividendType, filePath);
            }

            if (securityName == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.SecurityName, filePath);
            }

            if (isin is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Isin, filePath);
            }

            if (payment == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Payment, filePath);
            }

            if (currency == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Currency, filePath);
            }

            if (amount == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Amount, filePath);
            }

            if (dividendDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.DividendDate, filePath);
            }

            return new DividendFields(portfolioNumber,
                                      securityName,
                                      isin,
                                      dividendType,
                                      payment,
                                      amount,
                                      currency,
                                      dividendDate);
        }

        private static string GetPortfolioNumber(string line)
        {
            const int expectedWordNumber = 2;
            var lineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Exact);
            return lineComponents[expectedWordNumber - 1];
        }

        private static string GetDividendType(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Minimum);
            var dividendType = string.Join(" ", lineComponents.Skip(3));
            return dividendType switch
            {
                "Ordinary dividend" => "Ordinary",
                "Refund withholding tax" => "Tax Refund",
                _ => throw new UnsupportedValueException(ErrorFieldNames.DividendType, line, dividendType, ["Ordinary dividend", "Refund withholding tax"]),
            };
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
            var lineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Exact);
            return lineComponents[^1];
        }

        private static (decimal Payment, string Currency) GetPaymentAndCurrency(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Exact);
            var paymentString = lineComponents[^1].Replace("'", "");
            var currency = lineComponents[2];

            return decimal.TryParse(paymentString, out var payment) ? (payment, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.Payment, line, lineComponents.Length, paymentString);
        }

        private static decimal GetAmount(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            var amountString = lineComponents[^1].Replace("'", "");

            return decimal.TryParse(amountString, out var amount) ? amount : throw new ValueInvalidException(ErrorFieldNames.Amount, line, lineComponents.Length, amountString);
        }

        private static DateTime GetDividendDate(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 7, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 5;
            var dividendDateString = lineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(dividendDateString, out var dividendDate) ? dividendDate :
                       throw new ValueInvalidException(ErrorFieldNames.DividendDate, line, expectedWordNumber, dividendDateString);
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
        private static partial System.Text.RegularExpressions.Regex MultipleWhitespaceRegex();

        private readonly record struct DividendFields(
            string PortfolioNumber,
            string SecurityName,
            string Isin,
            string Type,
            decimal Payment,
            decimal Amount,
            string Currency,
            DateTime Date);
    }

    /// <summary>
    ///     Represents a dividend payment from a VIAC statement.
    /// </summary>
    /// <param name="PortfolioNumber">The portfolio number associated with the dividend.</param>
    /// <param name="SecurityName">The name of the security that paid the dividend.</param>
    /// <param name="Isin">The ISIN of the security that paid the dividend.</param>
    /// <param name="Units">Quantity of shares (amount ÷ payment).</param>
    /// <param name="Payment">Dividend per share.</param>
    /// <param name="Amount">Total received dividend.</param>
    /// <param name="Date">The date the dividend was credited to the account.</param>
    /// <param name="Remark">Notes about the dividend payment, including the type (e.g., Ordinary, Tax Refund) and currency conversion.</param>
    public readonly record struct Dividend(
        string PortfolioNumber,
        string SecurityName,
        string Isin,
        decimal Units,
        decimal Payment,
        decimal Amount,
        DateTime Date,
        string Remark);
}