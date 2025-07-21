namespace RtD.Utils
{
    public class Helpers
    {
        /// <summary>
        /// Escapes YAML " characters.
        /// </summary>
        /// <param name="value">String that will be checked for un-escaped " characters.</param>
        /// <returns>String with " characters escaped.</returns>
        public static string EscapeYaml(string? value)
        {
            return value?.Replace("\"", "\\\"") ?? string.Empty;
        }

        public static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}