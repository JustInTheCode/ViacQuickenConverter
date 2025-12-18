using System;
using System.Linq;

namespace ViacQuickenConverter.Viac.Error
{
    public class UnsupportedValueException : Exception
    {
        public UnsupportedValueException(string valueName, string line, string actualValue, string[] supportedValues) :
            base($"Unsupported {valueName} in line: '{line}'. Found: '{actualValue}'. Supported values: {string.Join(", ", supportedValues.Select(supportedValue => $"'{supportedValue}'"))}.")
        {
        }
    }
}