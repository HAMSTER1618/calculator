using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace calculator
{
    /// <summary>
    /// Pure operations and formatting (no UI state).
    /// </summary>
    public static class OperationManager
    {
        private const int MaxDisplayChars = 16;

        // Constraints similar to Windows Standard Calculator
        private const int MaxSigDigits = 16;

        // Use a non-breaking space as thousands separator to avoid line wrapping in UI
        private const char ThousandsSep = ' ';

        /// <summary>Add decimal separator if absent (uses comma for UI typing).</summary>
        public static string AppendDecimal(string display) =>
            display.Contains('.') ? display : display + ".";

        /// <summary>Append a digit, enforcing 16 significant digits limit.</summary>
        public static string AppendDigit(string display, string digit)
        {
            int digits = display.Count(char.IsDigit);
            if (digits >= MaxSigDigits) return display;
            return display == "0" ? digit : display + digit;
        }

        public static string Backspace(string display)
        {
            if (display.Length <= 1 || (display.Length == 2 && display.StartsWith('-')))
                return "0";
            return display[..^1];
        }

        /// <summary>Binary compute with basic ops and power.</summary>
        public static double Compute(double left, string op, double right, Action onError)
        {
            return op switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => Math.Abs(right) < 1e-15 ? ErrorReturn(onError) : left / right,
                "^" => Math.Pow(left, right),
                _ => left
            };
        }

        /// <summary>
        /// Format number with thousands separators and fallback to scientific if it doesn't fit.
        /// Decimal separator is '.' here; UI can replace with ',' if needed.
        /// </summary>
        public static string Format(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return "Chyba";

            string raw = x.ToString("G17", CultureInfo.InvariantCulture);
            if (NeedsScientific(raw))
                return x.ToString("G" + MaxSigDigits, CultureInfo.InvariantCulture)
                        .ToUpperInvariant();
            return GroupWithSpaces(raw);// now groups with spaces
        }

        // Formats the current user input string with thousands grouping.
        // Keeps the user's decimal separator (',' or '.') and does NOT trim trailing zeros.
        public static string FormatUserTyping(string s)
        {
            if (string.IsNullOrEmpty(s)) return "0";

            bool negative = s.StartsWith('-');
            // remove spaces used as group separators
            var t = (negative ? s[1..] : s).Replace(" ", "").Replace("\u00A0", "");

            // detect which decimal separator the user is using
            char decSep = t.Contains('.') ? '.' : '\0';

            string intPart;
            string fracPart = "";
            if (decSep == '\0')
            {
                intPart = t;
            }
            else
            {
                var parts = t.Split(decSep);
                intPart = parts[0];
                if (parts.Length > 1) fracPart = parts[1]; // keep as-is (no trimming)
            }

            string groupedInt = AddThousands(intPart);

            var sb = new StringBuilder();
            if (negative) sb.Append('-');
            sb.Append(groupedInt);
            if (decSep != '\0')
            {
                sb.Append(decSep);
                sb.Append(fracPart);
            }
            if (sb.Length == 0) return "0";
            return sb.ToString();
        }

        public static bool IsDigit(string token) => token.Length == 1 && char.IsDigit(token[0]);

        public static string NegateString(string display)
        {
            if (display == "0") return "0";
            return display.StartsWith('-') ? display[1..] : "-" + display;
        }

        /// <summary>Percent behavior matching Windows Standard.</summary>
        public static double PercentTransform(double acc, string pendingOp, double b)
        {
            if (pendingOp is "+" or "-") return acc * (b / 100.0);
            if (pendingOp == "*") return b / 100.0;
            if (pendingOp == "/") return b / 100.0;
            return b / 100.0;
        }

        public static string Reciprocal(double x, Action onError)
        {
            if (Math.Abs(x) < 1e-15) { onError(); return "Chyba"; }
            return Format(1.0 / x);
        }

        public static string Sqrt(double x, Action onError)
        {
            if (x < 0) { onError(); return "Chyba"; }
            return Format(Math.Sqrt(x));
        }

        public static string Square(double x) => Format(x * x);

        /// <summary>Parse user text accepting both ',' and '.' as decimal separators.</summary>
        // Be tolerant to spaces (normal and NBSP) and both decimal symbols
        public static bool TryParse(string s, out double value)
        {
            var t = s.Replace(" ", "").Replace("\u00A0", "");
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // ---------- helpers ----------

        // Group integer digits from the right, inserting the chosen space character
        private static string AddThousands(string digits)
        {
            var sb = new StringBuilder();
            int count = 0;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                sb.Insert(0, digits[i]);
                count++;
                if (count == 3 && i > 0)
                {
                    sb.Insert(0, ThousandsSep);
                    count = 0;
                }
            }
            return sb.ToString();
        }

        private static double ErrorReturn(Action onError)
        {
            onError();
            return double.NaN;
        }

        /// <summary>Group integer part by thousands and keep fractional part.</summary>
        private static string GroupWithSpaces(string raw)
        {
            bool negative = raw.StartsWith('-');
            if (negative) raw = raw[1..];

            var parts = raw.Split('.');
            string intPart = parts[0];
            string frac = parts.Length > 1 ? parts[1].TrimEnd('0') : "";

            string groupedInt = AddThousands(intPart);

            var sb = new StringBuilder();
            if (negative) sb.Append('-');
            sb.Append(groupedInt);
            if (frac.Length > 0)
            {
                sb.Append('.');
                sb.Append(frac);
            }

            return sb.ToString();
        }

        /// <summary>Return true if normal grouped form would exceed display width.</summary>
        private static bool NeedsScientific(string raw)
        {
            if (raw.Contains('E') || raw.Contains('e')) return true;

            var parts = raw.Split('.');
            string intPart = parts[0].TrimStart('-');
            string frac = parts.Length > 1 ? parts[1].TrimEnd('0') : "";

            string groupedInt = AddThousands(intPart);
            int total = groupedInt.Length + (frac.Length > 0 ? 1 + frac.Length : 0);
            return total > MaxDisplayChars;
        }
    }
}