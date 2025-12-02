namespace ViacTurboTaxConverter
{
    public class DividendPaymentParser
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
                return new Dividend(dividendFields.DividendName,
                                    dividendFields.DividendType,
                                    units,
                                    dividendFields.Payment,
                                    dividendFields.Amount,
                                    dividendFields.DividendDate,
                                    filePath);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(dividendFields.Currency, "USD", dividendFields.DividendDate);
            var usdPayment = dividendFields.Payment * exchangeRate;
            var usdAmount = dividendFields.Amount * exchangeRate;
            var remark = $"Converted from {dividendFields.Currency} to USD on {dividendFields.DividendDate:yyyy-MM-dd}. " +
                         $"Exchange rate: {exchangeRate:F6}. Original price: {dividendFields.Payment:F2}, amount: {dividendFields.Amount:F2}.";

            return new Dividend(dividendFields.DividendName,
                                dividendFields.DividendType,
                                units,
                                usdPayment,
                                usdAmount,
                                dividendFields.DividendDate,
                                filePath,
                                remark);
        }

        private static DividendFields ExtractDividendDetails(string text, string filePath)
        {
            var dividendType = DividendType.Unknown;
            string? dividendName = null;
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

                if (line.Contains("units") || line.Contains("Qty"))
                {
                    dividendName = GetDividendName(line);
                }

                if (line.Contains("Dividend payment:"))
                {
                    (payment, currency) = GetPaymentAndCurrency(line);
                }

                if (line.Contains("Amount") && !line.Contains("Amount credited:"))
                {
                    amount = GetAmount(line);
                }

                if (line.Contains("Amount credited:"))
                {
                    dividendDate = GetDividendDate(line);
                }
            }

            if (dividendType == DividendType.Unknown)
            {
                throw new ValueNotFoundException("dividend type", filePath);
            }

            if (dividendName is null)
            {
                throw new ValueNotFoundException("dividend name", filePath);
            }

            if (payment == 0)
            {
                throw new ValueNotFoundException("payment", filePath);
            }

            if (currency is null)
            {
                throw new ValueNotFoundException("currency", filePath);
            }

            if (amount == 0)
            {
                throw new ValueNotFoundException("amount", filePath);
            }

            if (dividendDate == default)
            {
                throw new ValueNotFoundException("dividend date", filePath);
            }

            return new DividendFields(dividendName,
                                      dividendType,
                                      payment,
                                      amount,
                                      currency,
                                      dividendDate);
        }

        private static DividendType GetDividendType(string line)
        {
            var dividendLineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Minimum);
            var dividendType = string.Join(" ", dividendLineComponents.Skip(3));

            return dividendType switch
            {
                "Ordinary dividend" => DividendType.Ordinary,
                "Refund withholding tax" => DividendType.TaxRefund,
                _ => throw new ValueInvalidException("dividend type", line),
            };
        }

        private static string GetDividendName(string line)
        {
            var unitsLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Minimum);
            var name = string.Join(" ", unitsLineComponents.Skip(2));
            return name.Length == 0 ? throw new ValueInvalidException(nameof(name), line) : name;
        }

        private static (decimal Payment, string Currency) GetPaymentAndCurrency(string line)
        {
            var paymentLineComponents = LineParser.SplitLine(line, 4, LineParser.WordCountRequirement.Exact);
            var currency = paymentLineComponents[2];
            var cleanedPayment = paymentLineComponents[3].Replace("'", "");
            return decimal.TryParse(cleanedPayment, out var payment) ? (payment, currency) : throw new ValueInvalidException(nameof(payment), line);
        }

        private static decimal GetAmount(string line)
        {
            var amountLineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Exact);
            var cleanedAmount = amountLineComponents[2].Replace("'", "");
            return decimal.TryParse(cleanedAmount, out var amount) ? amount : throw new ValueInvalidException(nameof(amount), line);
        }

        private static DateTime GetDividendDate(string line)
        {
            var dateLineComponents = LineParser.SplitLine(line, 7, LineParser.WordCountRequirement.Exact);
            return DateTime.TryParse(dateLineComponents[4], out var orderDate) ? orderDate : throw new ValueInvalidException(nameof(orderDate), line);
        }

        private readonly record struct DividendFields(
            string DividendName,
            DividendType DividendType,
            decimal Payment,
            decimal Amount,
            string Currency,
            DateTime DividendDate);
    }

    public readonly record struct Dividend(
        string SecurityName,
        DividendType DividendType,
        decimal Units,
        decimal Payment,
        decimal Amount,
        DateTime OrderDate,
        string FilePath,
        string? Remark = null);

    public enum DividendType
    {
        Unknown,

        Ordinary,

        TaxRefund,
    }
}