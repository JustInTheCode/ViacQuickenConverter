namespace ViacTurboTaxConverter
{
    public class ValueInvalidException : Exception
    {
        public ValueInvalidException(string valueName, string line) : base($"Line contains invalid {valueName}: '{line}'")
        {
        }
    }
}