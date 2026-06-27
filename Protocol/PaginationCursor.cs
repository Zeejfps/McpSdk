using System;
using System.Globalization;
using System.Text;

namespace McpSdk.Protocol
{
    /// <summary>
    /// Opaque pagination cursor helper (2025-11-25). The spec treats a list cursor as an opaque
    /// string the client must echo back verbatim and never parse. This SDK's built-in controllers
    /// page by integer offset, so we encode the offset behind a Base64 token: clients see only an
    /// opaque blob, while the server can still recover where the next page begins.
    ///
    /// User-implemented controllers are free to ignore this and mint their own opaque cursors; it is
    /// provided so the default controllers (and conformance tests) share one consistent scheme.
    /// </summary>
    public static class PaginationCursor
    {
        private const string OffsetPrefix = "offset:";

        /// <summary>Encodes a zero-based item offset as an opaque cursor token.</summary>
        public static string EncodeOffset(int offset)
        {
            var payload = OffsetPrefix + offset.ToString(CultureInfo.InvariantCulture);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        }

        /// <summary>
        /// Recovers the offset from a cursor previously produced by <see cref="EncodeOffset"/>.
        /// Returns <c>false</c> for a null/empty or malformed cursor so callers can decide how to
        /// treat a token they did not mint.
        /// </summary>
        public static bool TryDecodeOffset(string cursor, out int offset)
        {
            offset = 0;
            if (string.IsNullOrEmpty(cursor))
                return false;

            try
            {
                var text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                if (!text.StartsWith(OffsetPrefix, StringComparison.Ordinal))
                    return false;

                return int.TryParse(
                    text.Substring(OffsetPrefix.Length),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out offset);
            }
            catch (FormatException)
            {
                // Not valid Base64 — treat as an unrecognized cursor rather than throwing.
                return false;
            }
        }
    }
}
