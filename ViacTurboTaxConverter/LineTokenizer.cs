namespace ViacTurboTaxConverter
{
    public static class LineTokenizer
    {
        public enum WordCountRequirement
        {
            Exact,

            Minimum,
        }

        public static string[] GetWords(string line, int expectedWordCount, WordCountRequirement wordCountRequirement)
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
                base($"The line '{line}' should have exactly {expectedWordCount} words but has {actualWordCount} words.")
            {
            }
        }

        private class LineDoesNotHaveMinimumWordCountException : Exception
        {
            public LineDoesNotHaveMinimumWordCountException(string line, int expectedWordCount, int actualWordCount) :
                base($"The line '{line}' should have at least {expectedWordCount} words but has {actualWordCount} words.")
            {
            }
        }
    }
}