namespace ViacTurboTaxConverter
{
    public class ValueInvalidException : Exception
    {
        public ValueInvalidException(string line, string valueName) : base($"The line '{line}' does not contain a valid {valueName}.")
        {
        }
    }
}