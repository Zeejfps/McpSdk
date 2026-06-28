using System.Collections.Generic;

namespace McpSdk.Protocol
{
    /// <summary>
    /// MCP protocol version constants and the set of revisions this SDK can negotiate.
    /// </summary>
    public static class ProtocolVersion
    {
        /// <summary>The latest protocol revision this SDK implements.</summary>
        public const string Latest = "2025-11-25";

        /// <summary>
        /// All protocol revisions this SDK can interoperate with, newest first.
        /// </summary>
        public static readonly IReadOnlyList<string> Supported = new[]
        {
            "2025-11-25",
            "2025-06-18",
            "2025-03-26",
            "2024-11-05",
        };

        /// <summary>Returns true if <paramref name="version"/> is a revision this SDK supports.</summary>
        public static bool IsSupported(string version)
        {
            if (version == null)
                return false;

            foreach (var supported in Supported)
            {
                if (supported == version)
                    return true;
            }

            return false;
        }
    }
}
