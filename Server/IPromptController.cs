using System;
using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Server;

public interface IPromptController
{
    event Action ListChanged;
    bool IsListChangedNotificationSupported { get; }
    Task<ListPromptsResult> ListPrompts(ListPromptsRequest request);
    Task<GetPromptResult> GetPrompt(GetPromptRequest request);
}