using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ViacQuickenConverter.StatementParsing.CurrencyConversion;
using ViacQuickenConverter.StatementParsing.Error;
using ViacQuickenConverter.StatementParsing.Formatting;
using ViacQuickenConverter.StatementParsing.Text;

namespace ViacQuickenConverter.StatementParsing
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
            var fileName = Path.GetFileName(filePath);
            var remark = $"{dividendFields.Type}.";
            if (dividendFields.Currency == "USD")
            {
                return new Dividend(dividendFields.SecurityName,
                                    units,
                                    dividendFields.Payment,
                                    dividendFields.Amount,
                                    dividendFields.Date,
                                    fileName,
                                    remark);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(dividendFields.Currency, "USD", dividendFields.Date);
            var usdPayment = dividendFields.Payment * exchangeRate;
            var usdAmount = dividendFields.Amount * exchangeRate;
            remark += $" Converted from {dividendFields.Currency} to USD on {dividendFields.Date.ToString(DateFormats.Standard)} ({DateFormats.Standard}). " +
                      $"Exchange rate: {exchangeRate:F6}. Original dividend per share: {dividendFields.Payment:F2}, total received dividend: {dividendFields.Amount:F2}.";

            return new Dividend(dividendFields.SecurityName,
                                units,
                                usdPayment,
                                usdAmount,
                                dividendFields.Date,
                                fileName,
                                remark);
        }

        private static DividendFields ExtractDividendDetails(string text, string filePath)
        {
            string? dividendType = null;
            string? securityName = null;
            decimal payment = 0;
            string? currency = null;
            decimal amount = 0;
            DateTime dividendDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.StartsWith("Type of dividend:"))
                {
                    dividendType = GetDividendType(line);
                }
                else if (line.Contains("units") || line.Contains("Qty"))
                {
                    securityName = GetSecurityName(line);
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

            if (dividendType == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.DividendType, filePath);
            }

            if (securityName == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.SecurityName, filePath);
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

            return new DividendFields(securityName,
                                      dividendType,
                                      payment,
                                      amount,
                                      currency,
                                      dividendDate);
        }

        private static string GetDividendType(string line)
        {
            var dividendLineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Minimum);
            var dividendType = string.Join(" ", dividendLineComponents.Skip(3));
            string[] supportedTypes = ["Ordinary dividend", "Refund withholding tax"];

            return supportedTypes.Contains(dividendType) ? dividendType : throw new UnsupportedValueException(ErrorFieldNames.DividendType, line, dividendType, supportedTypes);
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

        private static (decimal Payment, string Currency) GetPaymentAndCurrency(string line)
        {
            var paymentLineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Exact);
            const int expectedPaymentWordNumber = 4;
            var paymentString = paymentLineComponents[expectedPaymentWordNumber - 1].Replace("'", "");
            var currency = paymentLineComponents[2];

            return decimal.TryParse(paymentString, out var payment) ? (payment, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.Payment, line, expectedPaymentWordNumber, paymentString);
        }

        private static decimal GetAmount(string line)
        {
            var amountLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 3;
            var amountString = amountLineComponents[expectedWordNumber - 1].Replace("'", "");

            return decimal.TryParse(amountString, out var amount) ? amount : throw new ValueInvalidException(ErrorFieldNames.Amount, line, expectedWordNumber, amountString);
        }

        private static DateTime GetDividendDate(string line)
        {
            var dateLineComponents = LineParser.SplitLine(line, 7, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 5;
            var dividendDateString = dateLineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(dividendDateString, out var dividendDate) ? dividendDate :
                       throw new ValueInvalidException(ErrorFieldNames.DividendDate, line, expectedWordNumber, dividendDateString);
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
        private static partial System.Text.RegularExpressions.Regex MultipleWhitespaceRegex();

        private readonly record struct DividendFields(
            string SecurityName,
            string Type,
            decimal Payment,
            decimal Amount,
            string Currency,
            DateTime Date);
    }

    /// <summary>
    ///     Represents a dividend payment from a VIAC statement.
    /// </summary>
    /// <param name="SecurityName">The name of the security that paid the dividend.</param>
    /// <param name="Units">Quantity of shares (amount ÷ payment).</param>
    /// <param name="Payment">Dividend per share.</param>
    /// <param name="Amount">Total received dividend.</param>
    /// <param name="Date">The date the dividend was credited to the account.</param>
    /// <param name="FileName">The name to the source file containing this dividend payment.</param>
    /// <param name="Remark">Notes about the dividend payment, including the type (e.g., Ordinary dividend, Refund withholding tax) and currency conversion details if applicable.</param>
    public readonly record struct Dividend(
        string SecurityName,
        decimal Units,
        decimal Payment,
        decimal Amount,
        DateTime Date,
        string FileName,
        string Remark);
}