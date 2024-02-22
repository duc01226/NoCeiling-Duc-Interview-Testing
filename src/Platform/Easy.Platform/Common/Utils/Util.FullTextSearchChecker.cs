using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Easy.Platform.Common.Utils;

/// <summary>
/// Utils is class to store all static small functions which could be used in any project.
/// This do not have any logic related to any domains.
/// Utils default grouping by "output", either by the output data type, or serve a "functional purpose".
/// Example: Utils.String should produce string as output.Utils.Enums should produce enum as output.Utils.Copy should only do the copy data functional.
/// </summary>
public static partial class Util
{
    public static class FullTextSearchChecker
    {
        /// <summary>
        /// Determines if the target text matches the search text.
        /// </summary>
        /// <param name="targetText">The text to be searched.</param>
        /// <param name="searchText">The text to search for.</param>
        /// <param name="exactMatchAllWords">If set to true, all words in the search text must exactly match in the target text. If false, any word in the search text can match in the target text.</param>
        /// <returns>Returns true if a match is found, otherwise false.</returns>
        public static bool IsFullTextSearchMatch(
            string targetText,
            string searchText,
            bool exactMatchAllWords = false)
        {
            if (targetText == null)
                return false;

            var noDiacriticsTargetText = RemoveDiacritics(targetText);
            var noDiacriticsSearchText = RemoveDiacritics(searchText);

            var searchWords = noDiacriticsSearchText.Trim().Split(" ");
            var isMatchWords = exactMatchAllWords
                ? searchWords.All(
                    word => Regex.IsMatch(
                        noDiacriticsTargetText,
                        GetMatchWordRegexPattern(word),
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                : searchWords.Any(
                    word => Regex.IsMatch(
                        noDiacriticsTargetText,
                        GetMatchWordRegexPattern(word),
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

            return Regex.IsMatch(
                       noDiacriticsTargetText,
                       $"{noDiacriticsSearchText}",
                       RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                   isMatchWords;
        }

        /// <summary>
        /// Removes diacritics from the given string.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <returns>The string with diacritics removed.</returns>
        public static string RemoveDiacritics(string str)
        {
            if (str == null)
                return null;

            // the normalization to FormD splits accented letters in letters+accents (Ex: "điện" => "d-ie^.n")
            // the rest removes those accents (and other non-spacing characters)
            // and creates a new string from the remaining chars.
            // Normalize again to FormC to compose char again to make "normal" text again, in case of there is some special mark char which
            // it's still match the where condition.
            return new string(
                    str.Normalize(NormalizationForm.FormD).ToCharArray().Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
                .Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Gets the regex pattern for matching a word in a string.
        /// </summary>
        /// <param name="word">The word to match.</param>
        /// <returns>The regex pattern for matching the word in a string.</returns>
        public static string GetMatchWordRegexPattern(string word)
        {
            return $"^(.*?(\\b{word}\\b)[^$]*)$";
        }
    }
}
