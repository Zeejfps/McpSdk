using System;
using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Server;

public interface IResourcesController
{
    event Action ListChanged;

    /// <summary>
    /// Raised when a subscribed resource changes, carrying the resource's URI. The server forwards it as
    /// a <c>notifications/resources/updated</c> notification. Only meaningful when
    /// <see cref="IsResourceChangedNotificationSupported"/> is true (i.e. the server advertises
    /// <c>resources.subscribe</c>).
    /// </summary>
    event Action<string> ResourceUpdated;

    bool? IsResourceChangedNotificationSupported { get; }
    bool? IsListChangedNotificationSupported { get; }

    Task<ListTemplatesResult> ListTemplates(ListTemplatesRequest request);
    Task<ListResourcesResult> ListResources(ListResourcesRequest request);
    Task<ReadResourceResult> ReadResource(ReadResourceRequest readResourceRequest);

    /// <summary>Registers interest in a resource so the client receives <c>resources/updated</c> for it.</summary>
    Task Subscribe(string uri);

    /// <summary>Cancels a prior <see cref="Subscribe"/> for the given URI.</summary>
    Task Unsubscribe(string uri);
}
