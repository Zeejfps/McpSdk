namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// The parameters of a server→client <c>elicitation/create</c> request (2025-11-25). A request is
    /// either <c>form</c> mode — carrying a restricted <see cref="RequestedSchema"/> describing the
    /// structured data to collect — or <c>url</c> mode — carrying a <see cref="Url"/> and
    /// <see cref="ElicitationId"/> for an out-of-band interaction. The <c>mode</c> field is optional
    /// for form mode and defaults to <see cref="ModeForm"/> when omitted.
    /// </summary>
    public sealed class ElicitRequest : IJsonObjectWriter
    {
        public const string ModeForm = "form";
        public const string ModeUrl = "url";

        /// <summary>Either <see cref="ModeForm"/> or <see cref="ModeUrl"/>. Defaults to form when omitted.</summary>
        public string Mode { get; }
        public string Message { get; }

        /// <summary>Form mode: the restricted schema describing the requested fields.</summary>
        public RequestedSchema RequestedSchema { get; }

        /// <summary>URL mode: the URL the user should be directed to.</summary>
        public string Url { get; }

        /// <summary>URL mode: a unique identifier the server uses to correlate completion.</summary>
        public string ElicitationId { get; }

        public bool IsUrlMode => Mode == ModeUrl;

        public ElicitRequest(IJsonObject jsonObject)
        {
            Mode = jsonObject["mode"]?.AsString();
            Message = jsonObject["message"]?.AsString();
            Url = jsonObject["url"]?.AsString();
            ElicitationId = jsonObject["elicitationId"]?.AsString();

            var schema = jsonObject["requestedSchema"]?.AsObject();
            if (schema != null)
                RequestedSchema = new RequestedSchema(schema);
        }

        private ElicitRequest(string mode, string message, RequestedSchema requestedSchema, string url, string elicitationId)
        {
            Mode = mode;
            Message = message;
            RequestedSchema = requestedSchema;
            Url = url;
            ElicitationId = elicitationId;
        }

        /// <summary>Builds a form-mode request.</summary>
        public ElicitRequest(string message, RequestedSchema requestedSchema)
            : this(ModeForm, message, requestedSchema, null, null)
        {
        }

        /// <summary>Builds a URL-mode request.</summary>
        public static ElicitRequest ForUrl(string message, string url, string elicitationId)
            => new(ModeUrl, message, null, url, elicitationId);

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("mode", Mode ?? ModeForm);
            writer.Write("message", Message);

            if (IsUrlMode)
            {
                writer.Write("url", Url);
                writer.Write("elicitationId", ElicitationId);
            }
            else if (RequestedSchema != null)
            {
                writer.Write("requestedSchema", RequestedSchema);
            }
        }
    }
}
