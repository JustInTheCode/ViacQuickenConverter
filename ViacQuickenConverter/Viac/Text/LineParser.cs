using System;

namespace ViacQuickenConverter.Viac.Text
{
    public static class LineParser
    {
        public enum WordCountRequirement
        {
            Exact,

            Minimum,
        }

        public static string[] SplitLine(string line, int expectedWordCount, WordCountRequirement wordCountRequirement, string context)
        {
            var lineComponents = line.Split(" ");
            return wordCountRequirement switch
            {
                WordCountRequirement.Exact when lineComponents.Length != expectedWordCount => throw new LineDoesNotHaveExactWordCountException(context,
                                                                                                  line,
                                                                                                  expectedWordCount,
                                                                                                  lineComponents.Length),
                WordCountRequirement.Minimum when lineComponents.Length < expectedWordCount => throw new LineDoesNotHaveMinimumWordCountException(context,
                                                                                                   line,
                                                                                                   expectedWordCount,
                                                                                                   lineComponents.Length),
                _ => lineComponents,
            };
        }

        private class LineDoesNotHaveExactWordCountException : Exception
        {
            public LineDoesNotHaveExactWordCountException(string context, string line, int expectedWordCount, int actualWordCount) :
                base($"Failed to extract {context}. Line '{line}' must have exactly {expectedWordCount} word(s) but has {actualWordCount}.")
            {
            }
        }

        private class LineDoesNotHaveMinimumWordCountException : Exception
        {
            public LineDoesNotHaveMinimumWordCountException(string context, string line, int expectedWordCount, int actualWordCount) :
                base($"Failed to extract {context}. Line '{line}' must have at least {expectedWordCount} word(s) but has {actualWordCount}.")
            {
            }
        }
    }
}