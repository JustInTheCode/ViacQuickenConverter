using System;
using System.IO;
using System.Threading.Tasks;
using ViacQuickenConverter.Formatting;
using ViacQuickenConverter.Viac.CurrencyConversion;
using ViacQuickenConverter.Viac.Error;
using ViacQuickenConverter.Viac.Text;

namespace ViacQuickenConverter.Viac
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
            var remark = $"Source file name: {Path.GetFileName(filePath)}. ";
            if (depositFields.Currency == "USD")
            {
                remark += "No currency conversion was necessary.";
                return new Deposit(depositFields.Payment, depositFields.Date, remark);
            }

            var exchangeRate = await _exchangeRateClient.GetExchangeRateAsync(depositFields.Currency, "USD", depositFields.Date);
            var usdPayment = depositFields.Payment * exchangeRate;
            remark += $"Converted from {depositFields.Currency} to USD on {depositFields.Date.ToString(DateFormats.Standard)} ({DateFormats.Standard}). " +
                      $"Exchange rate: {exchangeRate:F6}. Deposited: {depositFields.Payment:F2}";

            return new Deposit(usdPayment, depositFields.Date, remark);
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
                else if (line.Contains("Credit:"))
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
            var expectedPaymentWordNumber = paymentLineComponents.Length;
            var paymentString = paymentLineComponents[expectedPaymentWordNumber - 1].Replace("'", "");
            var currency = paymentLineComponents[^2];

            return decimal.TryParse(paymentString, out var payment) ? (payment, currency) :
                       throw new ValueInvalidException(ErrorFieldNames.Payment, line, expectedPaymentWordNumber, paymentString);
        }

        private static DateTime GetDepositDate(string line)
        {
            var dateLineComponents = LineParser.SplitLine(line, 6, LineParser.WordCountRequirement.Exact);
            const int expectedWordNumber = 4;
            var depositDateString = dateLineComponents[expectedWordNumber - 1];

            return DateTime.TryParse(depositDateString, out var depositDate) ? depositDate :
                       throw new ValueInvalidException(ErrorFieldNames.DepositDate, line, expectedWordNumber, depositDateString);
        }

        private readonly record struct DepositFields(decimal Payment, string Currency, DateTime Date);
    }

    /// <summary>
    ///     Represents a deposit transaction parsed from a VIAC statement.
    /// </summary>
    /// <param name="Payment">The amount received in the deposit transaction.</param>
    /// <param name="Date">The date the deposit was credited to the account.</param>
    /// <param name="Remark">Notes about the deposit, such as currency conversion details, exchange rates, or other relevant remarks.</param>
    public readonly record struct Deposit(decimal Payment, DateTime Date, string Remark);
}