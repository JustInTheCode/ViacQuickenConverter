using System;

namespace ViacQuickenConverter.Viac.Error
{
    public class ValueInvalidException : Exception
    {
        public ValueInvalidException(string valueName, string line, int expectedWordNumber, string actualValue) :
            base($"Failed to parse {valueName} from line: '{line}'. Expected word #{expectedWordNumber} to be a valid {valueName}, but found: '{actualValue}'.")
        {
        }
    }
}