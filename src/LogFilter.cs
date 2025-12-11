using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EstlCameo
{
    internal sealed class LogFilter
    {
        public char Key { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Include { get; set; } = true;
        public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.Basic;
        public string Pattern { get; set; } = string.Empty;
        public Color ForeColor { get; set; } = Color.Empty;
        public Color BackColor { get; set; } = Color.Empty;

        [Browsable(false)]
        public Regex CompiledRegex { get; set; }

        public override string ToString()
        {
            string kind = Include ? "Include" : "Exclude";
            return $"{Key}: {kind} / {MatchMode} / \"{Pattern}\"";
        }

        public bool IsMatch(string line)
        {
            if (string.IsNullOrEmpty(Pattern))
                return false;

            switch (MatchMode)
            {
                case FilterMatchMode.Basic:
                    return line.IndexOf(Pattern, StringComparison.OrdinalIgnoreCase) >= 0;

                case FilterMatchMode.Wildcard:
                    try
                    {
                        var regex = CompiledRegex ??= BuildWildcardRegex(Pattern);
                        return regex.IsMatch(line);
                    }
                    catch
                    {
                        return false;
                    }

                case FilterMatchMode.Regex:
                    try
                    {
                        var regex = CompiledRegex ??= new Regex(
                            Pattern,
                            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
                        return regex.IsMatch(line);
                    }
                    catch
                    {
                        return false;
                    }

                default:
                    return false;
            }
        }

        private static Regex BuildWildcardRegex(string pattern)
        {
            // Escape regex meta, then expand * and ?
            string escaped = Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".");
            return new Regex(escaped, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }
    }

}
