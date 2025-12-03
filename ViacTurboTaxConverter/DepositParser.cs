namespace ViacTurboTaxConverter
{
    public class DepositParser
    {
        private readonly ExchangeRateClient _exchangeRateClient;

        public DepositParser(ExchangeRateClient exchangeRateClient)
        {
            _exchangeRateClient = exchangeRateClient;
        }

        public async Task<Deposit> ParseAsync(string text, string filePath)
        {
            var depositFields = ExtractDepositDetails(text, filePath);
            if (depositFields.Currency == "USD")
            {
                return new Deposit(depositFields.Payment, depositFields.Date, filePath);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(depositFields.Currency, "USD", depositFields.Date);
            var usdPayment = depositFields.Payment * exchangeRate;
            var remark = $"Converted from {depositFields.Currency} to USD on {depositFields.Date.ToString(DateFormats.Standard)} ({DateFormats.Standard}). " +
                         $"Exchange rate: {exchangeRate:F6}. Deposited: {depositFields.Payment:F2}";

            return new Deposit(usdPayment, depositFields.Date, filePath, remark);
        }

        private static DepositFields ExtractDepositDetails(string text, string filePath)
        {
            decimal payment = 0;
            string? currency = null;
            DateTime depositDate = default;
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.Contains("Incoming payment:"))
                {
                    (payment, currency) = GetPaymentAndCurrency(line);
                }

                if (line.Contains("Credit:"))
                {
                    depositDate = GetDepositDate(line);
                }
            }

            if (payment == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Payment, filePath);
            }

            if (currency == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Currency, filePath);
            }

            if (depositDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.DepositDate, filePath);
            }

            return new DepositFields(payment, currency, depositDate);
        }

        private static (decimal Payment, string Currency) GetPaymentAndCurrency(string line)
        {
            var paymentLineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Minimum);
            var currency = paymentLineComponents[^2];
            var cleanedPayment = paymentLineComponents[^1].Replace("'", "");

            return decimal.TryParse(cleanedPayment, out var payment) ? (payment, currency) : throw new ValueInvalidException(ErrorFieldNames.Payment, line);
        }

        private static DateTime GetDepositDate(string line)
        {
            var dateLineComponents = LineParser.SplitLine(line, 6, LineParser.WordCountRequirement.Exact);

            return DateTime.TryParse(dateLineComponents[3], out var depositDate) ? depositDate : throw new ValueInvalidException(ErrorFieldNames.DepositDate, line);
        }

        private readonly record struct DepositFields(decimal Payment, string Currency, DateTime Date);
    }

    /// <summary>
    ///     Represents a deposit transaction parsed from a VIAC account statement.
    /// </summary>
    /// <param name="Payment">The amount received in the deposit transaction.</param>
    /// <param name="Date">The date the deposit was credited to the account.</param>
    /// <param name="FilePath">The file path of the source statement containing this deposit.</param>
    /// <param name="Remark">Optional notes about the deposit, such as currency conversion details, exchange rates, or other relevant remarks.</param>
    public readonly record struct Deposit(decimal Payment, DateTime Date, string FilePath, string? Remark = null);
}