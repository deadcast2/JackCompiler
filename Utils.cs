using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JackAnalyzer
{
    internal static class Utils
    {
        public const int MaxInt = 32767;

        public static string RemoveComments(string content)
        {
            return Regex.Replace(content, @"\/\*(.|\n)*?\*\/|\/\/(.*)", "").Trim();
        }

        public static bool InRange(this int _int, int min, int max)
        {
            return _int >= min && _int <= max;
        }

        public static string Without(this string _string, string and)
        {
            return _string.Replace(and, "");
        }

        public static IEnumerable<string> SplitAndNormalize(this string _string)
        {
            var normalized = Regex.Replace(_string, @"\r\n|\r|\n", "\n");

            return normalized.Split('\n').Select(p => p.Trim());
        }

        public static string ConvertToBase64(string unencoded)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(unencoded)).Without("=");
        }

        public static string ConvertFromBase64(string encoded)
        {
            var remainder = encoded.Length % 4;

            // Add padding if needed since padding removed so the inflator doesn't mess with strings.
            if (remainder != 0)
                encoded = encoded.PadRight(encoded.Length + (4 - encoded.Length % 4), '=');

            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
    }
}
