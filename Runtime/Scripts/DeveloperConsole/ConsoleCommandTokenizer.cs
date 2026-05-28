using System;
using System.Collections.Generic;
using System.Text;

namespace Machamy.DeveloperConsole
{
    public static class ConsoleCommandTokenizer
    {
        public static bool TryTokenizeForExecution(string input, out List<ConsoleToken> tokens, out string error)
        {
            tokens = new List<ConsoleToken>();
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return true;
            }

            bool inToken = false;
            bool inQuote = false;
            bool wasQuoted = false;
            bool escaping = false;
            char quoteChar = '\0';
            int tokenStart = -1;
            var sb = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (escaping)
                {
                    StartTokenIfNeeded(i);
                    sb.Append(c);
                    escaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    StartTokenIfNeeded(i);
                    escaping = true;
                    continue;
                }

                if (inQuote)
                {
                    if (c == quoteChar)
                    {
                        inQuote = false;
                        continue;
                    }

                    sb.Append(c);
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    StartTokenIfNeeded(i);
                    inQuote = true;
                    wasQuoted = true;
                    quoteChar = c;
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    if (inToken)
                    {
                        tokens.Add(new ConsoleToken(sb.ToString(), tokenStart, i, wasQuoted));
                        ResetToken();
                    }

                    continue;
                }

                StartTokenIfNeeded(i);
                sb.Append(c);
            }

            if (escaping)
            {
                error = "Escape character at end of command.";
                return false;
            }

            if (inQuote)
            {
                error = $"Unclosed {quoteChar} quote.";
                return false;
            }

            if (inToken)
            {
                tokens.Add(new ConsoleToken(sb.ToString(), tokenStart, input.Length, wasQuoted));
            }

            return true;

            void StartTokenIfNeeded(int index)
            {
                if (inToken) return;

                inToken = true;
                tokenStart = index;
            }

            void ResetToken()
            {
                inToken = false;
                wasQuoted = false;
                tokenStart = -1;
                sb.Clear();
            }
        }

        public static ConsoleCompletionParseResult TokenizeForCompletion(string input)
        {
            input ??= string.Empty;

            var tokens = new List<ConsoleToken>();
            bool inToken = false;
            bool inQuote = false;
            bool wasQuoted = false;
            bool escaping = false;
            char quoteChar = '\0';
            int tokenStart = -1;
            var sb = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (escaping)
                {
                    StartTokenIfNeeded(i - 1);
                    sb.Append(c);
                    escaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    StartTokenIfNeeded(i);
                    escaping = true;
                    continue;
                }

                if (inQuote)
                {
                    if (c == quoteChar)
                    {
                        inQuote = false;
                        continue;
                    }

                    sb.Append(c);
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    StartTokenIfNeeded(i);
                    inQuote = true;
                    wasQuoted = true;
                    quoteChar = c;
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    if (inToken)
                    {
                        tokens.Add(new ConsoleToken(sb.ToString(), tokenStart, i, wasQuoted));
                        ResetToken();
                    }

                    continue;
                }

                StartTokenIfNeeded(i);
                sb.Append(c);
            }

            if (escaping)
            {
                sb.Append('\\');
            }

            if (inToken)
            {
                tokens.Add(new ConsoleToken(sb.ToString(), tokenStart, input.Length, wasQuoted));
            }

            if (input.Length == 0 || char.IsWhiteSpace(input[^1]))
            {
                tokens.Add(new ConsoleToken(string.Empty, input.Length, input.Length, false));
            }

            int currentTokenIndex = Math.Max(0, tokens.Count - 1);
            return new ConsoleCompletionParseResult(tokens, currentTokenIndex, currentTokenIndex == 0, inQuote);

            void StartTokenIfNeeded(int index)
            {
                if (inToken) return;

                inToken = true;
                tokenStart = index;
            }

            void ResetToken()
            {
                inToken = false;
                wasQuoted = false;
                tokenStart = -1;
                sb.Clear();
            }
        }

        public static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            bool needsQuote = false;
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '\\')
                {
                    needsQuote = true;
                    break;
                }
            }

            if (!needsQuote)
            {
                return value;
            }

            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                if (c == '"' || c == '\\')
                {
                    sb.Append('\\');
                }

                sb.Append(c);
            }

            sb.Append('"');
            return sb.ToString();
        }
    }

    public readonly struct ConsoleToken
    {
        public ConsoleToken(string value, int startIndex, int endIndex, bool wasQuoted)
        {
            Value = value;
            StartIndex = startIndex;
            EndIndex = endIndex;
            WasQuoted = wasQuoted;
        }

        public string Value { get; }
        public int StartIndex { get; }
        public int EndIndex { get; }
        public bool WasQuoted { get; }
    }

    public readonly struct ConsoleCompletionParseResult
    {
        public ConsoleCompletionParseResult(IReadOnlyList<ConsoleToken> tokens, int currentTokenIndex, bool isCommandToken, bool hasUnclosedQuote)
        {
            Tokens = tokens;
            CurrentTokenIndex = currentTokenIndex;
            IsCommandToken = isCommandToken;
            HasUnclosedQuote = hasUnclosedQuote;
        }

        public IReadOnlyList<ConsoleToken> Tokens { get; }
        public int CurrentTokenIndex { get; }
        public bool IsCommandToken { get; }
        public bool HasUnclosedQuote { get; }
    }
}
