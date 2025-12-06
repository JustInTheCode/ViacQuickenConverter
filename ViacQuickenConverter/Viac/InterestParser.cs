using System;
using System.Threading.Tasks;
using ViacQuickenConverter.Viac.CurrencyConversion;
using ViacQuickenConverter.Viac.Error;
using ViacQuickenConverter.Viac.Text;

namespace ViacQuickenConverter.Viac
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
            if (interestFields.Credit == 0)
            {
                return new Interest(interestFields.PortfolioNumber, interestFields.Credit, interestFields.Date, "Amount is zero, no currency conversion necessary");
            }

            if (interestFields.Currency == "USD")
            {
                return new Interest(interestFields.PortfolioNumber, interestFields.Credit, interestFields.Date);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(interestFields.Currency, "USD", interestFields.Date);
            var usdCredit = interestFields.Credit * exchangeRate;
            var remark = $"{interestFields.Currency}-USD ({exchangeRate:F6}), Amt: {interestFields.Credit:F2}";

            return new Interest(interestFields.PortfolioNumber, usdCredit, interestFields.Date, remark);
        }

        private static InterestFields ExtractInterestDetails(string text, string filePath)
        {
            string? portfolioNumber = null;
            decimal? credit = null;
            string? currency = null;
            DateTime interestDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.StartsWith("Portfolio"))
                {
                    portfolioNumber = GetPortfolioNumber(line);
                }
                else if (line.Contains("Interest credit:"))
                {
                    (credit, currency) = GetCreditAndCurrency(line);
                }
                else if (line.Contains("we have credited you"))
                {
                    interestDate = GetInterestDate(line);
                }
            }

            if (portfolioNumber == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.PortfolioNumber, filePath);
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

            return new InterestFields(portfolioNumber, credit.Value, currency, interestDate);
        }

        private static string GetPortfolioNumber(string line)
        {
            const int expectedWordNumber = 2;
            var portfolioNumberLineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Exact);
            return portfolioNumberLineComponents[expectedWordNumber - 1];
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

        private readonly record struct InterestFields(string PortfolioNumber, decimal Credit, string Currency, DateTime Date);
    }

    /// <summary>
    ///     Represents an interest transaction parsed from a VIAC statement.
    /// </summary>
    /// <param name="PortfolioNumber">The portfolio number associated with the interest.</param>
    /// <param name="Credit">The interest amount credited to the account.</param>
    /// <param name="Date">The date the interest was credited to the account.</param>
    /// <param name="Remark">Notes about the currency conversion.</param>
    public readonly record struct Interest(string PortfolioNumber, decimal Credit, DateTime Date, string Remark = "Already in USD, no currency conversion necessary");
}