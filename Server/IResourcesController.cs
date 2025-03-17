using System;
using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Server;

public interface IResourcesController
{
    event Action ListChanged;
    event Action ResourceChanged;
    
    bool? IsResourceChangedNotificationSupported { get; }
    bool? IsListChangedNotificationSupported { get; }
    
    Task<ListTemplatesResult> ListTemplates(ListTemplatesRequest request);
    Task<ListResourcesResult> ListResources(ListResourcesRequest request);
    Task<ReadResourceResult> ReadResource(ReadResourceRequest readResourceRequest);
}