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
                return new Reimbursement(reimbursementFields.PortfolioNumber, reimbursementFields.Amount, reimbursementFields.Date);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(reimbursementFields.Currency, "USD", reimbursementFields.Date);
            var usdAmount = reimbursementFields.Amount * exchangeRate;
            var remark = $"{reimbursementFields.Currency}-USD ({exchangeRate:F6}), Amt: {reimbursementFields.Amount:F2}, Type: Reimbursement";

            return new Reimbursement(reimbursementFields.PortfolioNumber, usdAmount, reimbursementFields.Date, remark);
        }

        private static ReimbursementFields ExtractReimbursementDetails(string text, string filePath)
        {
            string? portfolioNumber = null;
            decimal amount = 0;
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
                    reimbursementDate = GetReimbursementDate(line);
                    (amount, currency) = GetAmountAndCurrency(line);
                    amount *= -1;
                }
            }

            if (portfolioNumber == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.PortfolioNumber, filePath);
            }

            if (amount == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Amount, filePath);
            }

            if (currency == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Currency, filePath);
            }

            if (reimbursementDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.ReimbursementDate, filePath);
            }

            return new ReimbursementFields(portfolioNumber, amount, currency, reimbursementDate);
        }

        private static string GetPortfolioNumber(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Exact, ErrorFieldNames.PortfolioNumber);
            return lineComponents[^1];
        }

        private static DateTime GetReimbursementDate(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 6, LineParser.WordCountRequirement.Exact, ErrorFieldNames.ReimbursementDate);
            const int expectedWordNumber = 4;
            var reimbursementDateString = lineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(reimbursementDateString, out var reimbursementDate) ? reimbursementDate :
                       throw new ValueInvalidException(ErrorFieldNames.ReimbursementDate, line, expectedWordNumber, reimbursementDateString);
        }

        private static (decimal Amount, string Currency) GetAmountAndCurrency(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Minimum, ErrorFieldNames.AmountAndCurrency);
            var amountString = lineComponents[^1].Replace("'", "");
            var currency = lineComponents[^2];

            return decimal.TryParse(amountString, out var amount) ? (amount, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.Amount, line, lineComponents.Length, amountString);
        }

        private readonly record struct ReimbursementFields(string PortfolioNumber, decimal Amount, string Currency, DateTime Date);
    }

    /// <summary>
    ///     Represents a reimbursement transaction parsed from a VIAC statement.
    /// </summary>
    /// <param name="PortfolioNumber">The portfolio number associated with the reimbursement.</param>
    /// <param name="Amount">The reimbursement amount debited from the 3a account.</param>
    /// <param name="Date">The date the reimbursement was debited from the account.</param>
    /// <param name="Remark">Notes about the currency conversion.</param>
    public readonly record struct Reimbursement(
        string PortfolioNumber,
        decimal Amount,
        DateTime Date,
        string Remark = "Already in USD, no currency conversion necessary, Type: Reimbursement");
}