namespace ViacQuickenConverter.StatementParsing.Text
{
    public static class LineParser
    {
        public enum WordCountRequirement
        {
            Exact,

            Minimum,
        }

        public static string[] SplitLine(string line, int expectedWordCount, WordCountRequirement wordCountRequirement)
        {
            var lineComponents = line.Split(" ");
            return wordCountRequirement switch
            {
                WordCountRequirement.Exact when lineComponents.Length != expectedWordCount => throw new LineDoesNotHaveExactWordCountException(line,
                                                                                                  expectedWordCount,
                                                                                                  lineComponents.Length),
                WordCountRequirement.Minimum when lineComponents.Length < expectedWordCount => throw new LineDoesNotHaveMinimumWordCountException(line,
                                                                                                   expectedWordCount,
                                                                                                   lineComponents.Length),
                _ => lineComponents,
            };
        }

        private class LineDoesNotHaveExactWordCountException : Exception
        {
            public LineDoesNotHaveExactWordCountException(string line, int expectedWordCount, int actualWordCount) :
                base($"Line '{line}' must have exactly {expectedWordCount} word(s) but has {actualWordCount}.")
            {
            }
        }

        private class LineDoesNotHaveMinimumWordCountException : Exception
        {
            public LineDoesNotHaveMinimumWordCountException(string line, int expectedWordCount, int actualWordCount) :
                base($"Line '{line}' must have at least {expectedWordCount} word(s) but has {actualWordCount}.")
            {
            }
        }
    }
}