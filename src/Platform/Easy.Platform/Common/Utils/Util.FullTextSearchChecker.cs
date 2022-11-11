using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Easy.Platform.Common.Utils;

/// <summary>
/// Utils is class to store all static small functions which could be used in any project.
/// This do not have any logic related to any domains.
///
/// Utils default grouping by "output", either by the output data type, or serve a "functional purpose".
/// Example: Utils.String should produce string as output.Utils.Enums should produce enum as output.Utils.Copy should only do the copy data functional.
/// </summary>
public static partial class Util
{
    public static class FullTextSearchChecker
    {
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

        public static string GetMatchWordRegexPattern(string word)
        {
            return $"^(.*?(\\b{word}\\b)[^$]*)$";
        }
    }
}
