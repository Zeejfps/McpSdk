using System.Text.RegularExpressions;

namespace McpSdk.Protocol
{
    /// <summary>
    /// Wire-framing rules shared by every transport (MCP 2025-11-25).
    ///
    /// <para>stdio framing: messages are newline-delimited UTF-8 and MUST NOT contain embedded
    /// newlines. Compact JSON never carries a raw CR/LF/TAB inside a string value (those are
    /// escaped as <c>\n</c>/<c>\r</c>/<c>\t</c> — two characters), so <see cref="ToSingleLine"/> only ever
    /// strips stray formatting and guarantees exactly one physical line per message.</para>
    ///
    /// <para>Batching: JSON-RPC batch support was removed in MCP 2025-06-18. A top-level array is no
    /// longer a valid frame; <see cref="IsBatch"/> lets the transport reject one explicitly instead
    /// of letting the JSON parser throw on it.</para>
    /// </summary>
    public static class JsonRpcFraming
    {
        /// <summary>The single byte that delimits messages on a stream. LF, never CRLF.</summary>
        public const char LineDelimiter = '\n';

        // Matches raw CR/LF/TAB control characters only — not their escaped (\n, \r, \t) JSON forms.
        private static readonly Regex EmbeddedControl = new("[\t\n\r]", RegexOptions.Compiled);

        /// <summary>
        /// Collapses a serialized message to a single physical line by removing any embedded
        /// CR/LF/TAB. Safe on compact JSON: legitimate newlines inside string values are escaped and
        /// therefore untouched.
        /// </summary>
        public static string ToSingleLine(string messageAsJson)
        {
            return messageAsJson == null ? null : EmbeddedControl.Replace(messageAsJson, string.Empty);
        }

        /// <summary>
        /// True when the message is a JSON-RPC batch (top-level array), which is unsupported since
        /// MCP 2025-06-18. Scans past leading insignificant whitespace and checks for an opening
        /// bracket without allocating or fully parsing.
        /// </summary>
        public static bool IsBatch(string messageAsJson)
        {
            if (messageAsJson == null)
                return false;

            foreach (var c in messageAsJson)
            {
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    continue;
                return c == '[';
            }

            return false;
        }
    }
}
