namespace ViacTurboTaxConverter
{
    public class ExchangeRateClient : IDisposable
    {
        private readonly HttpClient _httpClient = new();

        /// <inheritdoc />
        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime conversionDate)
        {
            var dateStr = conversionDate.ToString("yyyy-MM-dd");
            var url = $"https://api.frankfurter.app/{dateStr}?from={fromCurrency}&to={toCurrency}";

            var response = await _httpClient.GetStringAsync(url);
            var json = System.Text.Json.JsonDocument.Parse(response);

            return json.RootElement.GetProperty("rates").GetProperty(toCurrency).GetDecimal();
        }
    }
}