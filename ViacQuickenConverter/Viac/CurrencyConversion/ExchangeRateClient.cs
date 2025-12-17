using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ViacQuickenConverter.Viac.CurrencyConversion
{
    public class ExchangeRateClient : IDisposable
    {
        private readonly HttpClient _httpClient = new();

        private HashSet<string>? _supportedCurrencies;

        /// <inheritdoc />
        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime conversionDate)
        {
            try
            {
                _supportedCurrencies ??= await LoadSupportedCurrenciesAsync();
                if (!_supportedCurrencies.Contains(fromCurrency))
                {
                    throw new UnsupportedCurrencyException(fromCurrency);
                }

                if (!_supportedCurrencies.Contains(toCurrency))
                {
                    throw new UnsupportedCurrencyException(toCurrency);
                }

                var url = $"https://api.frankfurter.app/{conversionDate:yyyy-MM-dd}?from={fromCurrency}&to={toCurrency}";
                var response = await _httpClient.GetStringAsync(url);
                var json = System.Text.Json.JsonDocument.Parse(response);

                return json.RootElement.GetProperty("rates").GetProperty(toCurrency).GetDecimal();
            }
            catch (Exception exception) when (exception is not UnsupportedCurrencyException)
            {
                throw new CurrencyConversionFailedException(fromCurrency, toCurrency, conversionDate, exception.Message);
            }
        }

        private async Task<HashSet<string>> LoadSupportedCurrenciesAsync()
        {
            const string url = "https://api.frankfurter.dev/v1/currencies";
            var response = await _httpClient.GetStringAsync(url);
            var json = System.Text.Json.JsonDocument.Parse(response);
            return json.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet();
        }
    }

    public class UnsupportedCurrencyException : Exception
    {
        public UnsupportedCurrencyException(string currency) : base($"Currency '{currency}' is not supported. Please verify the currency code is valid.")
        {
        }
    }

    public class CurrencyConversionFailedException : Exception
    {
        public CurrencyConversionFailedException(string fromCurrency, string toCurrency, DateTime conversionDate, string errorMessage) :
            base($"Currency conversion API responded with error. Failed to convert {fromCurrency} to {toCurrency} on {conversionDate:yyyy-MM-dd}. " +
                 $"API Error: '{errorMessage}'. This is most likely an intermittent error, please try again later.")
        {
        }
    }
}