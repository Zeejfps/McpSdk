using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using McpSdk.Protocol.Models;

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

        /// <summary>
        /// Pages <paramref name="items"/> with this SDK's offset-cursor scheme and projects each paged item to
        /// its wire <see cref="Tool"/> via <paramref name="toTool"/>. An unrecognized/malformed
        /// <paramref name="cursor"/> falls back to the first page; the offset is clamped into range so a stale
        /// cursor (items removed since it was issued) yields an empty final page rather than throwing; a
        /// non-positive <paramref name="pageSize"/> (or <c>null</c>) means "no paging" — one page holding every
        /// item, with no <c>nextCursor</c>. Shared by <c>DefaultToolsController</c> and
        /// <c>CompositeToolsController</c> so their paging cannot drift.
        /// </summary>
        public static ListToolsResult GetPage<T>(IReadOnlyList<T> items, string cursor, int? pageSize, Func<T, Tool> toTool)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (toTool == null) throw new ArgumentNullException(nameof(toTool));

            var count = items.Count;

            var offset = 0;
            if (cursor != null && TryDecodeOffset(cursor, out var decoded))
                offset = decoded;
            if (offset < 0)
                offset = 0;
            if (offset > count)
                offset = count;

            var size = pageSize is > 0 ? pageSize.Value : count;
            var take = Math.Min(size, count - offset);

            var page = new Tool[take];
            for (var i = 0; i < take; i++)
                page[i] = toTool(items[offset + i]);

            var nextOffset = offset + take;
            var nextCursor = nextOffset < count ? EncodeOffset(nextOffset) : null;
            return new ListToolsResult(page, nextCursor);
        }
    }
}
