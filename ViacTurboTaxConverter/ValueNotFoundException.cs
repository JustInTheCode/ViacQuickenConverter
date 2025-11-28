namespace ViacTurboTaxConverter
{
    public class ValueNotFoundException : Exception
    {
        public ValueNotFoundException(string valueName, string filePath) : base($"Could not find the {valueName} in the file {filePath}.")
        {
        }
    }
}