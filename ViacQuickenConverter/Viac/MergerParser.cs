using System;
using System.Linq;
using ViacQuickenConverter.Viac.Error;
using ViacQuickenConverter.Viac.Text;

namespace ViacQuickenConverter.Viac
{
    public static partial class MergerParser
    {
        public static Merger Parse(string text, string filePath)
        {
            DateTime mergerDate = default;
            decimal? oldRatioUnits = null;
            decimal? newRatioUnits = null;
            string? oldIsin = null;
            string? newSecurityName = null;
            string? newIsin = null;
            var lineCount = 0;
            var lines = text.Split(Environment.NewLine);
            foreach (var line in lines)
            {
                if (line.Contains("Your portfolio holdings on"))
                {
                    mergerDate = GetMergerDate(line);
                }
                else if (line.StartsWith("Ratio:"))
                {
                    oldRatioUnits = GetOldRatioUnits(line);
                    newRatioUnits = GetNewRatioUnits(lines[lineCount + 2]);
                }
                else if (line.StartsWith("We take from your portfolio:"))
                {
                    oldIsin = GetIsin(lines[lineCount + 2]);
                }
                else if (line.StartsWith("We add to your portfolio:"))
                {
                    newSecurityName = GetSecurityName(lines[lineCount + 1]);
                    newIsin = GetIsin(lines[lineCount + 2]);
                }

                lineCount++;
            }

            if (oldIsin is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.NewIsin, filePath);
            }

            if (newSecurityName is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.OldSecurityName, filePath);
            }

            if (newIsin is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.OldIsin, filePath);
            }

            if (oldRatioUnits is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.OldRatioUnits, filePath);
            }

            if (newRatioUnits is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.NewRatioUnits, filePath);
            }

            if (mergerDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.MergerDate, filePath);
            }

            var conversionRatio = newRatioUnits.Value / oldRatioUnits.Value;
            if (conversionRatio != 1)
            {
                throw new UnsupportedConversionRatioException(conversionRatio, filePath);
            }

            return new Merger(oldIsin, newSecurityName, newIsin, mergerDate);
        }

        private static DateTime GetMergerDate(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 5, LineParser.WordCountRequirement.Exact, ErrorFieldNames.MergerDate);
            var mergerDateString = lineComponents[^1];
            mergerDateString = mergerDateString.TrimEnd(":").ToString();

            return DateTime.TryParse(mergerDateString, out var mergerDate) ? mergerDate :
                       throw new ValueInvalidException(ErrorFieldNames.MergerDate, line, lineComponents.Length, mergerDateString);
        }

        private static decimal GetOldRatioUnits(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 3, LineParser.WordCountRequirement.Minimum, ErrorFieldNames.OldRatioUnits);
            const int expectedWordNumber = 2;
            var ratioString = lineComponents[expectedWordNumber - 1];

            return decimal.TryParse(ratioString, out var ratio) ? ratio : throw new ValueInvalidException(ErrorFieldNames.OldRatioUnits, line, expectedWordNumber, ratioString);
        }

        private static decimal GetNewRatioUnits(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Minimum, ErrorFieldNames.NewRatioUnits);
            const int expectedWordNumber = 1;
            var ratioString = lineComponents[expectedWordNumber - 1];

            return decimal.TryParse(ratioString, out var ratio) ? ratio : throw new ValueInvalidException(ErrorFieldNames.OldRatioUnits, line, expectedWordNumber, ratioString);
        }

        private static string GetSecurityName(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Minimum, ErrorFieldNames.SecurityName);
            var name = string.Join(" ", lineComponents.Skip(1));
            if (name.Length == 0)
            {
                throw new ValueNotFoundException(ErrorFieldNames.SecurityName, line);
            }

            name = name.Replace("(old)", string.Empty);
            name = MultipleWhitespaceRegex().Replace(name, " ");

            return name.Trim();
        }

        private static string GetIsin(string line)
        {
            var lineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Exact, ErrorFieldNames.Isin);
            return lineComponents[^1];
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
        private static partial System.Text.RegularExpressions.Regex MultipleWhitespaceRegex();
    }

    public class UnsupportedConversionRatioException : Exception
    {
        public UnsupportedConversionRatioException(decimal actualRatio, string filePath) :
            base($"The conversion ratio {actualRatio} of the merger in file '{filePath}' is not supported. Only a ratio of 1 is supported. See documentation for details.")
        {
        }
    }

    /// <summary>
    ///     Represents a security merger transaction from a VIAC statement.
    /// </summary>
    /// <param name="OldIsin">The ISIN of the security that was removed from the portfolio.</param>
    /// <param name="NewSecurityName">The name of the security that was added to the portfolio.</param>
    /// <param name="NewIsin">The ISIN of the security that was added to the portfolio.</param>
    /// <param name="Date">The date the merger was executed.</param>
    public readonly record struct Merger(string OldIsin, string NewSecurityName, string NewIsin, DateTime Date);
}