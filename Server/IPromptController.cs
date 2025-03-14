using System;

namespace McpSdk.Server;

public interface IPromptController
{
    event Action ListChanged;
    bool IsListChangedNotificationSupported { get; }
}