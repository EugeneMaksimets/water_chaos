using System.Text;

namespace WaterChaos.Mission
{
    /// <summary>Text rules of the mission UI.</summary>
    public static class MissionText
    {
        /// <summary>
        /// True if the hint only repeats the objective title (it starts with the same words), so showing it
        /// under the objective list adds nothing. Case, punctuation and extra spaces are ignored.
        /// </summary>
        public static bool HintRepeatsTitle(string title, string hint)
        {
            string normalTitle = Normalize(title);
            string normalHint = Normalize(hint);
            if (normalTitle.Length == 0 || normalHint.Length == 0)
                return false;

            return normalHint.StartsWith(normalTitle, System.StringComparison.Ordinal);
        }

        // Lower-case letters and digits, words separated by single spaces.
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (space && builder.Length > 0)
                        builder.Append(' ');
                    builder.Append(char.ToLowerInvariant(c));
                    space = false;
                }
                else
                {
                    space = true;
                }
            }

            return builder.ToString();
        }
    }
}
