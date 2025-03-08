using System.Text;
using System.Text.Json;
using McpSharp.Client;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

class SystemJson : IJson
{
    public string Stringify(JsonRpcRequest<int, InitializeRequestPayload> jsonRpcRequest)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(memoryStream);
        writer.WriteStartObject();
        {
            writer.WriteString("jsonrpc", jsonRpcRequest.JsonRpcVersion);
            writer.WriteNumber("id", jsonRpcRequest.Id);
            writer.WriteString("method", jsonRpcRequest.Method);
            writer.WriteStartObject("params");
            {
                var parameters = jsonRpcRequest.Parameters;
                writer.WriteString("protocolVersion", parameters.ProtocolVersion);
                
                writer.WriteStartObject("capabilities");
                {
                    var capabilities = parameters.Capabilities;
                    if (capabilities.Roots != null)
                    {
                        writer.WriteStartObject("roots");
                        writer.WriteBoolean("listChanged", capabilities.Roots.IsListChangedNotificationSupported);
                        writer.WriteEndObject();
                    }

                    if (capabilities.Sampling != null)
                    {
                        writer.WriteStartObject("sampling");
                        writer.WriteEndObject();
                    }
                }
                writer.WriteEndObject();

                writer.WriteStartObject("clientInfo");
                {
                    var clientInfo = parameters.ClientInfo;
                    writer.WriteString("name", clientInfo.Name);
                    writer.WriteString("version", clientInfo.Version);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        
        writer.Flush();

        var jsonString = Encoding.UTF8.GetString(memoryStream.ToArray());
        return jsonString;
    }

    public string Stringify(JsonRpcNotification jsonRpcNotification)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(memoryStream);
        
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", jsonRpcNotification.JsonRpcVersion);
        writer.WriteString("method", jsonRpcNotification.Method);
        writer.WriteEndObject();
        
        writer.Flush();
        
        var jsonString = Encoding.UTF8.GetString(memoryStream.ToArray());
        return jsonString;
    }

    public string Stringify(JsonRpcRequest<int, ListToolsRequestPayload> jsonRpcRequest)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(memoryStream);
        writer.WriteStartObject();
        {
            writer.WriteString("jsonrpc", jsonRpcRequest.JsonRpcVersion);
            writer.WriteNumber("id", jsonRpcRequest.Id);
            writer.WriteString("method", jsonRpcRequest.Method);
        }
        writer.WriteEndObject();
        
        writer.Flush();
        
        var jsonString = Encoding.UTF8.GetString(memoryStream.ToArray());
        return jsonString;
    }

    public void Parse(string jsonString, out JsonRpcResponse<int, InitializeResponsePayload?> jsonRpcResponse)
    {
        // Load the JSON into a JsonDocument.
        using JsonDocument document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;
        
        var rpcVersion = root.GetProperty("jsonrpc").GetString();
        var id = root.GetProperty("id").GetInt32();

        InitializeResponsePayload? result = null;
        JsonRpcResponseError? error = null;
        if (root.TryGetProperty("result", out var resultObj))
        {
            var protocolVersion = resultObj.GetProperty("protocolVersion").GetString();
            
            var capabilitiesObj = resultObj.GetProperty("capabilities");
            var capabilities = new ServerCapabilities();
            
            if (capabilitiesObj.TryGetProperty("prompts", out var prompts))
            {
                var promptsListChanged = false;
                if (prompts.TryGetProperty("listChanged", out var listChangedProp))
                {
                    promptsListChanged = listChangedProp.GetBoolean();
                }
                capabilities.Prompts = new PromptsCapability(promptsListChanged);
            }

            if (capabilitiesObj.TryGetProperty("tools", out var toolsObj))
            {
                var toolsListChanged = false;
                if (toolsObj.TryGetProperty("listChanged", out var listChangedProp))
                    toolsListChanged = listChangedProp.GetBoolean();
                
                capabilities.Tools = new ToolsCapability(toolsListChanged);
            }
            
            var serverInfoObj = resultObj.GetProperty("serverInfo");
            var serverName = serverInfoObj.GetProperty("name").GetString();
            var serverVersion = serverInfoObj.GetProperty("version").GetString();

            var serverInfo = new ServerInfo(serverName, serverVersion);
            result = new InitializeResponsePayload(protocolVersion, capabilities, serverInfo);
        }
        else if (root.TryGetProperty("error", out var errorObj))
        {
            var code = errorObj.GetProperty("code").GetInt32();
            var message = errorObj.GetProperty("message").GetString();
            error = new JsonRpcResponseError(code, message, null);
        }
        
        jsonRpcResponse = new JsonRpcResponse<int, InitializeResponsePayload?>(rpcVersion, id, result, error);
    }

    public void Parse(string jsonString, out JsonRpcResponse<int, ListToolsResultPayload?> jsonRpcResponse)
    {
        using JsonDocument document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;
        
        var rpcVersion = root.GetProperty("jsonrpc").GetString();
        var id = root.GetProperty("id").GetInt32();

        ListToolsResultPayload? result = null;
        JsonRpcResponseError? error = null;

        if (root.TryGetProperty("result", out var resultObj))
        {
            var toolsObj = resultObj.GetProperty("tools");
            var toolsCount = toolsObj.GetArrayLength();
            var toolInfos = new ToolInfo[toolsCount];
            for (var i = 0; i < toolsCount; i++)
            {
                var toolObj = toolsObj[i];
                var toolName = toolObj.GetProperty("name").GetString();
                var toolDescription = toolObj.GetProperty("description").GetString();
                toolInfos[i] = new ToolInfo(toolName, toolDescription);
            }
            
            result = new ListToolsResultPayload(toolInfos);
        }
        else if (root.TryGetProperty("error", out var errorObj))
        {
            var code = errorObj.GetProperty("code").GetInt32();
            var message = errorObj.GetProperty("message").GetString();
            error = new JsonRpcResponseError(code, message, null);
        }
        
        jsonRpcResponse = new JsonRpcResponse<int, ListToolsResultPayload?>(rpcVersion, id, result, error);
    }
}