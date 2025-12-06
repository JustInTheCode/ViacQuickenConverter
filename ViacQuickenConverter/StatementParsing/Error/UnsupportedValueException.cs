namespace ViacQuickenConverter.StatementParsing.Error
{
    public class UnsupportedValueException : Exception
    {
        public UnsupportedValueException(string valueName, string line, string actualValue, params string[] supportedValues) :
            base($"Unsupported {valueName} in line: '{line}'. Found: '{actualValue}'. Supported values: {string.Join(", ", supportedValues.Select(v => $"'{v}'"))}.")
        {
        }
    }
}