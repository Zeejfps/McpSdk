namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// The client's reply to an <c>elicitation/create</c> request (2025-11-25). The three-action model
    /// distinguishes an explicit submit (<see cref="ActionAccept"/>, carrying <see cref="Content"/> for
    /// form mode), an explicit refusal (<see cref="ActionDecline"/>), and a dismissal without choice
    /// (<see cref="ActionCancel"/>). For URL mode, an accept carries no content.
    /// </summary>
    public sealed class ElicitResult : IJsonObjectWriter
    {
        public const string ActionAccept = "accept";
        public const string ActionDecline = "decline";
        public const string ActionCancel = "cancel";

        public string Action { get; }

        /// <summary>The submitted data, present only for a form-mode accept. Opaque flat primitive object.</summary>
        public IJsonObject Content { get; }

        public ElicitResult(IJsonObject jsonObject)
        {
            Action = jsonObject["action"]?.AsString();
            Content = jsonObject["content"]?.AsObject();
        }

        public ElicitResult(string action, IJsonObject content = null)
        {
            Action = action;
            Content = content;
        }

        /// <summary>The user submitted form data.</summary>
        public static ElicitResult Accept(IJsonObject content) => new(ActionAccept, content);

        /// <summary>The user consented to a URL-mode interaction (no content is returned).</summary>
        public static ElicitResult AcceptUrl() => new(ActionAccept);

        /// <summary>The user explicitly declined the request.</summary>
        public static ElicitResult Decline() => new(ActionDecline);

        /// <summary>The user dismissed the request without making a choice.</summary>
        public static ElicitResult Cancel() => new(ActionCancel);

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("action", Action);
            Content?.WriteTo(writer, "content");
        }
    }
}
