using System;
using McpSdk.Protocol;

namespace McpSdk.Server;

public abstract class ReferenceModel
{
    public abstract void AsJson(IJsonWriter jsonWriter);

    public static ReferenceModel FromJsonObject(IJsonObject jsonObject)
    {
        var type = jsonObject["type"].AsString();
        if (type == "ref/resource")
        {
            return new ResourceReference(jsonObject);
        }
        
        if (type == "ref/prompt")
        {
            return new PromptReference(jsonObject);
        }
        
        throw new Exception($"Unknown reference type: {type}");
    }
}