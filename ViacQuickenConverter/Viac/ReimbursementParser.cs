using System;
using System.Threading.Tasks;
using ViacQuickenConverter.Viac.CurrencyConversion;
using ViacQuickenConverter.Viac.Error;
using ViacQuickenConverter.Viac.Text;

namespace ViacQuickenConverter.Viac
{
    public class ReimbursementParser
    {
        private readonly ExchangeRateClient _exchangeRateClient;

        public ReimbursementParser(ExchangeRateClient exchangeRateClient)
        {
            _exchangeRateClient = exchangeRateClient;
        }

        public async Task<Reimbursement> ParseAsync(string text, string filePath)
        {
            var reimbursementFields = ExtractReimbursementDetails(text, filePath);
            if (reimbursementFields.Currency == "USD")
            {
                return new Reimbursement(reimbursementFields.PortfolioNumber, reimbursementFields.Value, reimbursementFields.Date);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(reimbursementFields.Currency, "USD", reimbursementFields.Date);
            var usdValue = reimbursementFields.Value * exchangeRate;
            var remark = $"{reimbursementFields.Currency}-USD ({exchangeRate:F6}), Amt: {reimbursementFields.Value:F2}, Type: Reimbursement";

            return new Reimbursement(reimbursementFields.PortfolioNumber, usdValue, reimbursementFields.Date, remark);
        }

        private static ReimbursementFields ExtractReimbursementDetails(string text, string filePath)
        {
            string? portfolioNumber = null;
            decimal value = 0;
            string? currency = null;
            DateTime reimbursementDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.StartsWith("Portfolio"))
                {
                    portfolioNumber = GetPortfolioNumber(line);
                }
                else if (line.Contains("Reimbursement:"))
                {
                    (value, currency) = GetReimbursementAndCurrency(line);
                    reimbursementDate = GetReimbursementDate(line);
                }
            }

            if (portfolioNumber == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.PortfolioNumber, filePath);
            }

            if (value == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Reimbursement, filePath);
            }

            if (currency == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Currency, filePath);
            }

            if (reimbursementDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.ReimbursementDate, filePath);
            }

            return new ReimbursementFields(portfolioNumber, value, currency, reimbursementDate);
        }

        private static string GetPortfolioNumber(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Exact, ErrorFieldNames.PortfolioNumber);
            return lineComponents[^1];
        }

        private static (decimal Reimbursement, string Currency) GetReimbursementAndCurrency(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Minimum, ErrorFieldNames.ReimbursementAndCurrency);
            var reimbursementString = lineComponents[^1].Replace("'", "");
            var currency = lineComponents[^2];

            return decimal.TryParse(reimbursementString, out var reimbursement) ? (reimbursement, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.Reimbursement, line, lineComponents.Length, reimbursementString);
        }

        private static DateTime GetReimbursementDate(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 6, LineParser.WordCountRequirement.Exact, ErrorFieldNames.ReimbursementDate);
            const int expectedWordNumber = 4;
            var reimbursementDateString = lineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(reimbursementDateString, out var reimbursementDate) ? reimbursementDate :
                       throw new ValueInvalidException(ErrorFieldNames.ReimbursementDate, line, expectedWordNumber, reimbursementDateString);
        }

        private readonly record struct ReimbursementFields(string PortfolioNumber, decimal Value, string Currency, DateTime Date);
    }

    /// <summary>
    ///     Represents a reimbursement transaction parsed from a VIAC statement.
    /// </summary>
    /// <param name="PortfolioNumber">The portfolio number associated with the reimbursement.</param>
    /// <param name="Value">The reimbursement received.</param>
    /// <param name="Date">The date the reimbursement was debited from the account.</param>
    /// <param name="Remark">Notes about the currency conversion.</param>
    public readonly record struct Reimbursement(
        string PortfolioNumber,
        decimal Value,
        DateTime Date,
        string Remark = "Already in USD, no currency conversion necessary, Type: Reimbursement");
}