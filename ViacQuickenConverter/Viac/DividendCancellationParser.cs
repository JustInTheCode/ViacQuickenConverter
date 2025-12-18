using System;
using System.Linq;
using System.Threading.Tasks;
using ViacQuickenConverter.Viac.CurrencyConversion;
using ViacQuickenConverter.Viac.Error;
using ViacQuickenConverter.Viac.Text;

namespace ViacQuickenConverter.Viac
{
    public partial class DividendCancellationParser
    {
        private readonly ExchangeRateClient _exchangeRateClient;

        public DividendCancellationParser(ExchangeRateClient exchangeRateClient)
        {
            _exchangeRateClient = exchangeRateClient;
        }

        public async Task<DividendCancellation> ParseAsync(string text, string filePath)
        {
            var dividendFields = ExtractDividendDetails(text, filePath);
            var amount = dividendFields.Amount * -1;
            if (dividendFields.Currency == "USD")
            {
                return new DividendCancellation(dividendFields.PortfolioNumber, dividendFields.SecurityName, dividendFields.Isin, amount, dividendFields.Date);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(dividendFields.Currency, "USD", dividendFields.Date);
            var usdAmount = amount * exchangeRate;
            var remark = $"{dividendFields.Currency}-USD ({exchangeRate:F6}), Amt: {amount:F2}, Type: Cancellation";

            return new DividendCancellation(dividendFields.PortfolioNumber,
                                            dividendFields.SecurityName,
                                            dividendFields.Isin,
                                            usdAmount,
                                            dividendFields.Date,
                                            remark);
        }

        private static DividendCancellationFields ExtractDividendDetails(string text, string filePath)
        {
            string? portfolioNumber = null;
            string? securityName = null;
            string? isin = null;
            string? currency = null;
            decimal amount = 0;
            DateTime dividendDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.StartsWith("Portfolio"))
                {
                    portfolioNumber = GetPortfolioNumber(line);
                }
                else if (line.Contains("units") || line.Contains("Qty"))
                {
                    securityName = GetSecurityName(line);
                }
                else if (line.Contains("ISIN:"))
                {
                    isin = GetIsin(line);
                }
                else if (line.Contains("Amount") && !line.Contains("Amount debited:"))
                {
                    (amount, currency) = GetAmountAndCurrency(line);
                }
                else if (line.Contains("Amount debited:"))
                {
                    dividendDate = GetDividendDate(line);
                }
            }

            if (portfolioNumber == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.PortfolioNumber, filePath);
            }

            if (securityName == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.SecurityName, filePath);
            }

            if (isin is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Isin, filePath);
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

            return new DividendCancellationFields(portfolioNumber,
                                                  securityName,
                                                  isin,
                                                  amount,
                                                  currency,
                                                  dividendDate);
        }

        private static string GetPortfolioNumber(string line)
        {
            const int expectedWordNumber = 2;
            var lineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Exact, ErrorFieldNames.PortfolioNumber);
            return lineComponents[expectedWordNumber - 1];
        }

        private static string GetSecurityName(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Minimum, ErrorFieldNames.SecurityName);
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
            var lineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Exact, ErrorFieldNames.Isin);
            return lineComponents[^1];
        }

        private static (decimal Amount, string Currency) GetAmountAndCurrency(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact, ErrorFieldNames.AmountAndCurrency);
            var amountString = lineComponents[^1].Replace("'", "");
            var currency = lineComponents[^2];

            return decimal.TryParse(amountString, out var amount) ? (amount, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.Amount, line, lineComponents.Length, amountString);
        }

        private static DateTime GetDividendDate(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 7, LineParser.WordCountRequirement.Exact, ErrorFieldNames.DividendDate);
            const int expectedWordNumber = 5;
            var dividendDateString = lineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(dividendDateString, out var dividendDate) ? dividendDate :
                       throw new ValueInvalidException(ErrorFieldNames.DividendDate, line, expectedWordNumber, dividendDateString);
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
        private static partial System.Text.RegularExpressions.Regex MultipleWhitespaceRegex();

        private readonly record struct DividendCancellationFields(
            string PortfolioNumber,
            string SecurityName,
            string Isin,
            decimal Amount,
            string Currency,
            DateTime Date);
    }

    /// <summary>
    ///     Represents a dividend cancellation from a VIAC statement.
    /// </summary>
    /// <param name="PortfolioNumber">The portfolio number associated with the dividend cancellation.</param>
    /// <param name="SecurityName">The name of the security that had the dividend canceled.</param>
    /// <param name="Isin">The ISIN of the security that had the dividend canceled.</param>
    /// <param name="Amount">Total cancelled dividend amount (negative value).</param>
    /// <param name="Date">The value date of the dividend cancellation (typically matches the original dividend payment date).</param>
    /// <param name="Remark">Notes about the dividend cancellation, including the type (Cancellation) and currency conversion.</param>
    public readonly record struct DividendCancellation(
        string PortfolioNumber,
        string SecurityName,
        string Isin,
        decimal Amount,
        DateTime Date,
        string Remark = "Already in USD, no currency conversion necessary, Type: Cancellation");
}