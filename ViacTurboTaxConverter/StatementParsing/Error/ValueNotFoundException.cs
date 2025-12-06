namespace ViacTurboTaxConverter.StatementParsing.Error
{
    public class ValueNotFoundException : Exception
    {
        public ValueNotFoundException(string valueName, string filePath) : base($"Value '{valueName}' was not found in file `{filePath}`.")
        {
        }
    }
}