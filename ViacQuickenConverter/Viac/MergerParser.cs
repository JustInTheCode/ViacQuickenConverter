using System;
using System.Linq;
using ViacQuickenConverter.Viac.Error;
using ViacQuickenConverter.Viac.Text;

namespace ViacQuickenConverter.Viac
{
    public partial class MergerParser
    {
        public static Merger Parse(string text, string filePath)
        {
            string? portfolioNumber = null;
            DateTime mergerDate = default;
            decimal? ratio = null;
            string? oldSecurityName = null;
            string? oldIsin = null;
            string? newSecurityName = null;
            string? newIsin = null;
            var lineCount = 0;
            var lines = text.Split(Environment.NewLine);
            foreach (var line in lines)
            {
                if (line.StartsWith("Portfolio"))
                {
                    portfolioNumber = GetPortfolioNumber(line);
                }
                else if (line.Contains("Your portfolio holdings on"))
                {
                    mergerDate = GetMergerDate(line);
                }
                else if (line.StartsWith("Ratio:"))
                {
                    ratio = GetRatio(line);
                }
                else if (line.StartsWith("We take from your portfolio:"))
                {
                    oldSecurityName = GetSecurityName(lines[lineCount + 1]);
                    oldIsin = GetIsin(lines[lineCount + 2]);
                }
                else if (line.StartsWith("We add to your portfolio:"))
                {
                    newSecurityName = GetSecurityName(lines[lineCount + 1]);
                    newIsin = GetIsin(lines[lineCount + 2]);
                }

                lineCount++;
            }

            if (portfolioNumber == null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.PortfolioNumber, filePath);
            }

            if (oldSecurityName is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.NewSecurityName, filePath);
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

            if (ratio is null)
            {
                throw new ValueNotFoundException(ErrorFieldNames.Ratio, filePath);
            }

            if (mergerDate == default)
            {
                throw new ValueNotFoundException(ErrorFieldNames.MergerDate, filePath);
            }

            return new Merger(portfolioNumber,
                              oldSecurityName,
                              oldIsin,
                              newSecurityName,
                              newIsin,
                              ratio.Value,
                              mergerDate);
        }

        private static string GetPortfolioNumber(string line)
        {
            const int expectedWordNumber = 2;
            var portfolioNumberLineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Exact);
            return portfolioNumberLineComponents[expectedWordNumber - 1];
        }

        private static DateTime GetMergerDate(string line)
        {
            const int expectedWordNumber = 5;
            var dateLineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Exact);
            var mergerDateString = dateLineComponents[expectedWordNumber - 1];
            mergerDateString = mergerDateString.TrimEnd(":").ToString();

            return DateTime.TryParse(mergerDateString, out var mergerDate) ? mergerDate :
                       throw new ValueInvalidException(ErrorFieldNames.MergerDate, line, expectedWordNumber, mergerDateString);
        }

        private static decimal GetRatio(string line)
        {
            const int expectedWordNumber = 2;
            var portfolioNumberLineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Minimum);
            var ratioString = portfolioNumberLineComponents[1];

            return decimal.TryParse(ratioString, out var ratio) ? ratio : throw new ValueInvalidException(ErrorFieldNames.Ratio, line, expectedWordNumber, ratioString);
        }

        private static string GetSecurityName(string line)
        {
            var unitsLineComponents = LineParser.SplitLine(line, 2, LineParser.WordCountRequirement.Minimum);
            var name = string.Join(" ", unitsLineComponents.Skip(1));
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
            const int expectedWordNumber = 2;
            var isinLineComponents = LineParser.SplitLine(line, expectedWordNumber, LineParser.WordCountRequirement.Exact);
            return isinLineComponents[expectedWordNumber - 1];
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
        private static partial System.Text.RegularExpressions.Regex MultipleWhitespaceRegex();
    }

    public readonly record struct Merger(
        string PortfolioNumber,
        string OldSecurityName,
        string OldIsin,
        string NewSecurityName,
        string NewIsin,
        decimal Ratio,
        DateTime Date);
}