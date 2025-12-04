namespace ViacTurboTaxConverter
{
    public class ExchangeRateClient : IDisposable
    {
        private readonly HttpClient _httpClient = new();

        /// <inheritdoc />
        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime conversionDate)
        {
            var url = $"https://api.frankfurter.app/{conversionDate:yyyy-MM-dd}?from={fromCurrency}&to={toCurrency}";
            var response = await _httpClient.GetStringAsync(url);
            var json = System.Text.Json.JsonDocument.Parse(response);

            return json.RootElement.GetProperty("rates").GetProperty(toCurrency).GetDecimal();
        }
    }
}