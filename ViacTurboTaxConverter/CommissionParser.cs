namespace ViacTurboTaxConverter
{
    public class CommissionParser
    {
        private readonly ExchangeRateClient _exchangeRateClient;

        public CommissionParser(ExchangeRateClient exchangeRateClient)
        {
            _exchangeRateClient = exchangeRateClient;
        }

        public async Task<Commission> ParseAsync(string text, string filePath)
        {
            var commissionFields = ExtractCommissionDetails(text, filePath);
            var fileName = Path.GetFileName(filePath);
            if (commissionFields.ChargedAmount == 0)
            {
                return new Commission(commissionFields.ChargedAmount,
                                      commissionFields.Date,
                                      fileName,
                                      "The charged amount is 0. This entry is included for completeness, as some statements may legitimately have a zero commission. " +
                                      "Please double-check to ensure this is correct.");
            }

            if (commissionFields.Currency == "USD")
            {
                return new Commission(commissionFields.ChargedAmount, commissionFields.Date, fileName);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(commissionFields.Currency, "USD", commissionFields.Date);
            var usdChargedAmount = commissionFields.ChargedAmount * exchangeRate;
            var remark = $"Converted from {commissionFields.Currency} to USD on {commissionFields.Date.ToString(DateFormats.Standard)} ({DateFormats.Standard}). " +
                         $"Exchange rate: {exchangeRate:F6}. Charged amount: {commissionFields.ChargedAmount:F2}";

            return new Commission(usdChargedAmount, commissionFields.Date, fileName, remark);
        }

        private static CommissionFields ExtractCommissionDetails(string text, string filePath)
        {
            decimal? chargedAmount = null;
            string? currency = null;
            DateTime commissionDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.Contains("Charged amount:"))
                {
                    (chargedAmount, currency) = GetChargedAndCurrency(line);
                }
                else if (line.Contains("we have debited your account:"))
                {
                    commissionDate = GetCommissionDate(line);
                }
            }

            if (chargedAmount == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.ChargedAmount, filePath);
            }

            if (currency == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Currency, filePath);
            }

            if (commissionDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.CommissionDate, filePath);
            }

            return new CommissionFields(chargedAmount.Value, currency, commissionDate);
        }

        private static (decimal ChargedAmount, string Currency) GetChargedAndCurrency(string line)
        {
            var chargedLineComponents = LineParser.SplitLine(line, 6, LineParser.WordCountRequirement.Minimum);
            var expectedChargedWordNumber = chargedLineComponents.Length;
            var chargedAmountString = chargedLineComponents[expectedChargedWordNumber - 1].Replace("'", "");
            var currency = chargedLineComponents[^2];

            return decimal.TryParse(chargedAmountString, out var chargedAmount) ? (chargedAmount, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.ChargedAmount, line, expectedChargedWordNumber, chargedAmountString);
        }

        private static DateTime GetCommissionDate(string line)
        {
            var dateLineComponents = LineParser.SplitLine(line, 7, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 2;
            var commissionDateString = dateLineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(commissionDateString, out var commissionDate) ? commissionDate :
                       throw new ValueInvalidException(ErrorFieldNames.CommissionDate, line, expectedWordNumber, commissionDateString);
        }

        private readonly record struct CommissionFields(decimal ChargedAmount, string Currency, DateTime Date);
    }

    /// <summary>
    ///     Represents a commission transaction parsed from a VIAC statement.
    /// </summary>
    /// <param name="ChargedAmount">The commission amount charged to the account.</param>
    /// <param name="Date">The date the commission was debited from the account.</param>
    /// <param name="FileName">The file name of the source statement containing this commission transaction.</param>
    /// <param name="Remark">Optional notes about the commission, such as currency conversion details, exchange rates, or other relevant remarks.</param>
    public readonly record struct Commission(decimal ChargedAmount, DateTime Date, string FileName, string? Remark = null);
}