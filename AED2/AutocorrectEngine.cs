using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AED2
{
    public static class AutocorrectEngine
    {
        private static readonly HashSet<string> Words =
            new HashSet<string>(StringComparer.Ordinal);

        private static readonly Dictionary<int, List<string>> WordsByLength =
            new Dictionary<int, List<string>>();

        private static readonly Dictionary<string, List<string>> WordsByStripped =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

        public static int Count { get { return Words.Count; } }

        public static bool IsLoaded { get { return Words.Count > 0; } }

        public static int LoadDictionary(string filePath)
        {
            Words.Clear();
            WordsByLength.Clear();
            WordsByStripped.Clear();

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Add(line);
                }
            }

            return Words.Count;
        }

        public static void Add(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;

            string normalized = Normalize(word);
            if (!Words.Add(normalized)) return;

            List<string> bucket;
            if (!WordsByLength.TryGetValue(normalized.Length, out bucket))
            {
                bucket = new List<string>();
                WordsByLength[normalized.Length] = bucket;
            }
            bucket.Add(normalized);

            string stripped = RemoveDiacritics(normalized);
            List<string> variants;
            if (!WordsByStripped.TryGetValue(stripped, out variants))
            {
                variants = new List<string>();
                WordsByStripped[stripped] = variants;
            }
            variants.Add(normalized);
        }

        public static bool TryFixAccents(string input, out string corrected)
        {
            corrected = input;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string normalized = Normalize(input);
            if (Words.Contains(normalized)) return false;

            List<string> variants;
            if (!WordsByStripped.TryGetValue(RemoveDiacritics(normalized), out variants)) return false;

            string only = null;
            foreach (string variant in variants)
            {
                if (variant == normalized) continue;
                if (only != null) return false;
                only = variant;
            }

            if (only == null) return false;

            corrected = MatchCase(input, only);
            return true;
        }

        public static bool HasSuggestion(string input, int maxDistance = 2)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            string normalized = Normalize(input);
            if (Words.Contains(normalized)) return false;

            for (int length = normalized.Length - maxDistance;
                 length <= normalized.Length + maxDistance;
                 length++)
            {
                List<string> bucket;
                if (length < 1 || !WordsByLength.TryGetValue(length, out bucket)) continue;

                foreach (string candidate in bucket)
                {
                    if (SpellChecker.GetEditDistanceWithin(normalized, candidate, maxDistance) <= maxDistance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string decomposed = text.Normalize(NormalizationForm.FormD);
            StringBuilder builder = new StringBuilder(decomposed.Length);

            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        public static bool IsKnown(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            return Words.Contains(Normalize(word));
        }

        public static string Correct(string input, int maxDistance = 2)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            string normalized = Normalize(input);

            if (Words.Contains(normalized)) return input;

            string bestMatch = null;
            int lowestDistance = int.MaxValue;

            for (int length = normalized.Length - maxDistance;
                 length <= normalized.Length + maxDistance;
                 length++)
            {
                List<string> bucket;
                if (length < 1 || !WordsByLength.TryGetValue(length, out bucket)) continue;

                foreach (string candidate in bucket)
                {
                    int limit = Math.Min(maxDistance, lowestDistance - 1);
                    if (limit < 0) break;

                    int distance = SpellChecker.GetEditDistanceWithin(normalized, candidate, limit);

                    if (distance <= limit)
                    {
                        lowestDistance = distance;
                        bestMatch = candidate;

                        if (lowestDistance == 1) break;
                    }
                }

                if (lowestDistance == 1) break;
            }

            if (bestMatch == null) return input;

            return MatchCase(input, bestMatch);
        }

        public static List<string> Suggest(string input, int maxDistance = 2, int count = 5)
        {
            List<string> suggestions = new List<string>();
            if (string.IsNullOrWhiteSpace(input)) return suggestions;

            string normalized = Normalize(input);
            List<KeyValuePair<string, int>> matches = new List<KeyValuePair<string, int>>();

            for (int length = normalized.Length - maxDistance;
                 length <= normalized.Length + maxDistance;
                 length++)
            {
                List<string> bucket;
                if (length < 1 || !WordsByLength.TryGetValue(length, out bucket)) continue;

                foreach (string candidate in bucket)
                {
                    int distance = SpellChecker.GetEditDistanceWithin(normalized, candidate, maxDistance);
                    if (distance <= maxDistance && distance > 0)
                    {
                        matches.Add(new KeyValuePair<string, int>(candidate, distance));
                    }
                }
            }

            return matches
                .OrderBy(m => m.Value)
                .ThenBy(m => m.Key, StringComparer.Create(Culture, true))
                .Take(count)
                .Select(m => MatchCase(input, m.Key))
                .ToList();
        }

        private static string Normalize(string word)
        {
            return word.Trim().ToLower(Culture);
        }

        private static string MatchCase(string input, string result)
        {
            string trimmed = input.Trim();
            if (trimmed.Length == 0) return result;

            if (trimmed.Length > 1 && trimmed == trimmed.ToUpper(Culture))
            {
                return result.ToUpper(Culture);
            }

            if (char.IsUpper(trimmed[0]))
            {
                return char.ToUpper(result[0], Culture) + result.Substring(1);
            }

            return result;
        }
    }
}
