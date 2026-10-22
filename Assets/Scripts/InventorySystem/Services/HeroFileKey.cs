using System;
using System.IO;
using System.Text;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The store key of a hero's file: <c>Name_id</c>, so a folder listing says which hero is which and the
    /// id still pins the identity. The name part is only for the eye - it is cut down to what a file name
    /// allows - so the id, after the last underscore, is the only part ever read back. A file written
    /// before names were part of the key is the bare id and reads as one.
    /// </summary>
    public static class HeroFileKey
    {
        private const char Separator = '_';
        private const string FallbackName = "Hero";
        private const int MaxNameLength = 32;

        /// <summary>The key for the hero called <paramref name="name"/> under <paramref name="id"/>.</summary>
        public static string Compose(string name, string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("A hero file needs an id.", nameof(id));

            return Slug(name) + Separator + id;
        }

        /// <summary>The id a key carries: what follows the last underscore, or the whole key for a bare-id file.</summary>
        public static string IdOf(string key)
        {
            var separator = key.LastIndexOf(Separator);

            return separator < 0 || separator == key.Length - 1 ? key : key[(separator + 1)..];
        }

        // A name as a file-name part: whitespace runs become one hyphen, characters a file name cannot hold are
        // dropped, the length is capped, and a name that leaves nothing is "Hero". The id keeps two heroes with
        // the same name apart, and a reserved device name is safe because the key is never the name alone.
        private static string Slug(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var slug = new StringBuilder();

            foreach (var character in (name ?? string.Empty).Trim())
            {
                if (slug.Length >= MaxNameLength)
                    break;

                if (char.IsWhiteSpace(character))
                {
                    if (slug.Length > 0 && slug[^1] != '-')
                        _ = slug.Append('-');
                }
                else if (!char.IsControl(character) && Array.IndexOf(invalid, character) < 0)
                {
                    _ = slug.Append(character);
                }
            }

            // Windows drops a trailing dot or space from a file name, which would change the key.
            var result = slug.ToString().TrimEnd('.', '-', ' ');

            return result.Length == 0 ? FallbackName : result;
        }
    }
}
