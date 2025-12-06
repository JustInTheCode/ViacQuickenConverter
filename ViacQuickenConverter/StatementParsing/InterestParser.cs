using System;
using System.IO;
using System.Threading.Tasks;
using ViacQuickenConverter.StatementParsing.CurrencyConversion;
using ViacQuickenConverter.StatementParsing.Error;
using ViacQuickenConverter.StatementParsing.Formatting;
using ViacQuickenConverter.StatementParsing.Text;

namespace ViacQuickenConverter.StatementParsing
{
    public class InterestParser
    {
        private readonly ExchangeRateClient _exchangeRateClient;

        public InterestParser(ExchangeRateClient exchangeRateClient)
        {
            _exchangeRateClient = exchangeRateClient;
        }

        public async Task<Interest> ParseAsync(string text, string filePath)
        {
            var interestFields = ExtractInterestDetails(text, filePath);
            var fileName = Path.GetFileName(filePath);
            if (interestFields.Credit == 0)
            {
                return new Interest(interestFields.Credit,
                                    interestFields.Date,
                                    filePath,
                                    "Interest credited is 0. This entry is included for completeness, as some statements may legitimately have zero interest credited. " +
                                    "Please double-check to ensure this is correct.");
            }

            if (interestFields.Currency == "USD")
            {
                return new Interest(interestFields.Credit, interestFields.Date, fileName);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(interestFields.Currency, "USD", interestFields.Date);
            var usdCredit = interestFields.Credit * exchangeRate;
            var remark = $"Converted from {interestFields.Currency} to USD on {interestFields.Date.ToString(DateFormats.Standard)} ({DateFormats.Standard}). " +
                         $"Exchange rate: {exchangeRate:F6}. Interest credit: {interestFields.Credit:F2}";

            return new Interest(usdCredit, interestFields.Date, fileName, remark);
        }

        private static InterestFields ExtractInterestDetails(string text, string filePath)
        {
            decimal? credit = null;
            string? currency = null;
            DateTime interestDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.Contains("Interest credit:"))
                {
                    (credit, currency) = GetCreditAndCurrency(line);
                }
                else if (line.Contains("we have credited you"))
                {
                    interestDate = GetInterestDate(line);
                }
            }

            if (credit == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.InterestCredit, filePath);
            }

            if (currency == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Currency, filePath);
            }

            if (interestDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.InterestDate, filePath);
            }

            return new InterestFields(credit.Value, currency, interestDate);
        }

        private static (decimal Credit, string Currency) GetCreditAndCurrency(string line)
        {
            var creditLineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Exact);
            const int expectedCreditWordNumber = 4;
            var creditString = creditLineComponents[expectedCreditWordNumber - 1].Replace("'", "");
            var currency = creditLineComponents[2];

            return decimal.TryParse(creditString, out var credit) ? (credit, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.InterestCredit, line, expectedCreditWordNumber, creditString);
        }

        private static DateTime GetInterestDate(string line)
        {
            var dateLineComponents = LineParser.SplitLine(line, 6, LineParser.WordCountRequirement.Minimum);
            const int expectedWordNumber = 2;
            var interestDateString = dateLineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(interestDateString, out var interestDate) ? interestDate :
                       throw new ValueInvalidException(ErrorFieldNames.InterestDate, line, expectedWordNumber, interestDateString);
        }

        private readonly record struct InterestFields(decimal Credit, string Currency, DateTime Date);
    }

    /// <summary>
    ///     Represents an interest transaction parsed from a VIAC statement.
    /// </summary>
    /// <param name="Credit">The interest amount credited to the account.</param>
    /// <param name="Date">The date the interest was credited to the account.</param>
    /// <param name="FileName">The file name of the source statement containing this interest transaction.</param>
    /// <param name="Remark">Optional notes about the interest credit, such as currency conversion details, exchange rates, or other relevant remarks.</param>
    public readonly record struct Interest(decimal Credit, DateTime Date, string FileName, string? Remark = null);
}