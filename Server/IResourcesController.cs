using System;

namespace McpSdk.Server;

public interface IResourcesController
{
    event Action ListChanged;
    event Action ResourceChanged;
    bool? IsResourceChangedNotificationSupported { get; }
    bool? IsListChangedNotificationSupported { get; }
}