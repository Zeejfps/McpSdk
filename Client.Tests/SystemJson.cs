using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using McpSharp.Client;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace Client.Tests;

internal class SystemJson : IJson
{
    public string Stringify(JsonRpcRequest<int, InitializeRequestPayload> jsonRpcRequest)
    {
        using var reqWriter = BeginWrite(jsonRpcRequest);

        var paramWriter = reqWriter.WriteStartParams();
        {
            var parameters = jsonRpcRequest.Parameters;
            paramWriter.WriteString("protocolVersion", parameters.ProtocolVersion);
            
            paramWriter.WriteStartObject("capabilities");
            {
                var capabilities = parameters.Capabilities;
                if (capabilities.Roots != null)
                {
                    paramWriter.WriteStartObject("roots");
                    paramWriter.WriteBoolean("listChanged", capabilities.Roots.IsListChangedNotificationSupported);
                    paramWriter.WriteEndObject();
                }

                if (capabilities.Sampling != null)
                {
                    paramWriter.WriteStartObject("sampling");
                    paramWriter.WriteEndObject();
                }
            }
            paramWriter.WriteEndObject();

            paramWriter.WriteStartObject("clientInfo");
            {
                var clientInfo = parameters.ClientInfo;
                paramWriter.WriteString("name", clientInfo.Name);
                paramWriter.WriteString("version", clientInfo.Version);
            }
            paramWriter.WriteEndObject();
        }
        reqWriter.WriteEndParams();
        
        return reqWriter.EndWrite();
    }

    public string Stringify(JsonRpcRequest<int, CallToolRequestPayload> jsonRpcRequest)
    {
        using var rpcWriter = BeginWrite(jsonRpcRequest);

        var paramWriter = rpcWriter.WriteStartParams();
        {
            var parameters = jsonRpcRequest.Parameters;
            paramWriter.WriteString("name", parameters.ToolName);
            paramWriter.WriteStartObject("arguments");
            {
                var arguments = parameters.Arguments;
                foreach (var (key, value) in arguments)
                {
                    paramWriter.WriteUnknown(key, value);
                }
            }
            paramWriter.WriteEndObject();
        }
        rpcWriter.WriteEndParams();
        
        return rpcWriter.EndWrite(); 
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
        using var reqWriter = BeginWrite(jsonRpcRequest);
        return reqWriter.EndWrite();
    }

    private JsonRpcRequestWriter BeginWrite(JsonRpcRequest<int> jsonRpcRequest)
    {
        return JsonRpcRequestWriter.BeginWrite(jsonRpcRequest);
    }

    public void Parse(string jsonString, out JsonRpcResponse<int, InitializeResultPayload?> jsonRpcResponse)
    {
        // Load the JSON into a JsonDocument.
        using var document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;
        
        var rpcVersion = root.GetProperty("jsonrpc").GetString();
        var id = root.GetProperty("id").GetInt32();

        InitializeResultPayload? result = null;
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
            result = new InitializeResultPayload(protocolVersion, capabilities, serverInfo);
        }

        var error = TryParseError(root);
        jsonRpcResponse = new JsonRpcResponse<int, InitializeResultPayload?>(rpcVersion, id, result, error);
    }

    public void Parse(string jsonString, out JsonRpcResponse<int, ListToolsResultPayload?> jsonRpcResponse)
    {
        using var document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;
        
        var rpcVersion = root.GetProperty("jsonrpc").GetString();
        var id = root.GetProperty("id").GetInt32();

        ListToolsResultPayload? result = null;

        if (root.TryGetProperty("result", out var resultObj))
        {
            var toolsObj = resultObj.GetProperty("tools");
            var toolsCount = toolsObj.GetArrayLength();
            var toolInfos = new Tool[toolsCount];
            for (var i = 0; i < toolsCount; i++)
            {
                var toolObj = toolsObj[i];
                var toolName = toolObj.GetProperty("name").GetString();
                var toolDescription = toolObj.GetProperty("description").GetString();
                var inputSchema = toolObj.GetProperty("inputSchema").GetSchema();
                toolInfos[i] = new Tool(toolName, toolDescription, inputSchema);
            }
            
            result = new ListToolsResultPayload(toolInfos);
        }

        var error = TryParseError(root);
        jsonRpcResponse = new JsonRpcResponse<int, ListToolsResultPayload?>(rpcVersion, id, result, error);
    }

    public void Parse(string jsonString, [UnscopedRef] out JsonRpcResponse<int, CallToolResultPayload> jsonRpcResponse)
    {
        using JsonDocument document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;

        var rpcVersion = root.GetProperty("jsonrpc").GetString();
        var id = root.GetProperty("id").GetInt32();

        TryParseResult(root, out CallToolResultPayload result);
        var error = TryParseError(root);
        jsonRpcResponse = new JsonRpcResponse<int, CallToolResultPayload>(rpcVersion, id, result, error);
    }

    public IJsonObject Parse(string text)
    {
        throw new NotImplementedException();
    }

    public string Stringify(Action<IJsonWriter> json)
    {
        throw new NotImplementedException();
    }

    private bool TryParseResult(JsonElement root, out CallToolResultPayload result)
    {
        if (!root.TryGetProperty("result", out var resultObj))
        {
            result = null;
            return false;
        }

        Content[] content;
        if (resultObj.TryGetProperty("content", out var contentArray))
        {
            var contentItemsCount = contentArray.GetArrayLength();
            content = new Content[contentItemsCount];
            for (var i = 0; i < contentItemsCount; i++)
            {
                Content? contentItem = null;
                var contentItemObj = contentArray[i];
                var type = contentItemObj.GetProperty("type").GetString();
                switch (type)
                {
                    case "text":
                        var text = contentItemObj.GetProperty("text").GetString();
                        contentItem = new TextContent(text);
                        break;
                    case "image":
                        var data = contentItemObj.GetProperty("data").GetString();
                        var mimeType = contentItemObj.GetProperty("mimeType").GetString();
                        contentItem = new ImageContent(mimeType, data);
                        break;
                    case "resource":
                        var resourceObj = contentItemObj.GetProperty("resource");
                        var uri = resourceObj.GetProperty("uri").GetString();
                        mimeType = resourceObj.GetProperty("mimeType").GetString();
                        text = resourceObj.GetProperty("text").GetString();
                        contentItem = new ResourceContent(uri, mimeType, text);
                        break;
                }
                content[i] = contentItem;
            }
        }
        else
        {
            content = [];
        }

        var isError = false;
        if (root.TryGetProperty("error", out var errorObj))
        {
            isError = errorObj.GetBoolean();
        }
        result = new CallToolResultPayload(content, isError);
        return true;
    }

    private JsonRpcResponseError? TryParseError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var errorObj))
            return null;

        var code = errorObj.GetProperty("code").GetInt32();
        var message = errorObj.GetProperty("message").GetString();
        return new JsonRpcResponseError(code, message, null);
    }
}

public sealed class JsonWriter : IJsonWriter
{
    private readonly Utf8JsonWriter _jsonWriter;

    public JsonWriter(Utf8JsonWriter jsonWriter)
    {
        _jsonWriter = jsonWriter;
    }

    public IJsonWriter Write(string propertyName, string value)
    {
        _jsonWriter.WriteString(propertyName, value);
        return this;
    }

    public IJsonWriter Write(string propertyName, string[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteStringValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, double value)
    {
        _jsonWriter.WriteNumberValue(value);
        return this;
    }

    public IJsonWriter Write(string propertyName, double[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteNumberValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, int value)
    {
        _jsonWriter.WriteNumberValue(value);
        return this;
    }

    public IJsonWriter Write(string propertyName, int[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteNumberValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, float value)
    {
        _jsonWriter.WriteNumberValue(value);
        return this;
    }

    public IJsonWriter Write(string propertyName, float[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteNumberValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, bool value)
    {
        _jsonWriter.WriteBoolean(propertyName, value);
        return this;
    }

    public IJsonWriter Write(string propertyName, bool[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteBooleanValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, Action<IJsonWriter> obj)
    {
        _jsonWriter.WriteStartObject();
        obj(this);
        _jsonWriter.WriteEndObject();
        return this;
    }

    public IJsonWriter Write(string propertyName, Action<IJsonWriter>[] objs)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var obj in objs)
        {
            _jsonWriter.WriteStartObject();
            obj(this);
            _jsonWriter.WriteEndObject();
        }
        _jsonWriter.WriteEndArray();
        return this;
    }
}

public static class JsonDomExtensions
{
    public static void WriteUnknown(this Utf8JsonWriter writer, string name, object value)
    {
        var valueType = value.GetType();
        if (valueType.IsPrimitive || valueType == typeof(string))
        {
            writer.WritePrimitive(name, value);
        }
    }
    
    public static void WritePrimitive(this Utf8JsonWriter writer, string name, object value)
    {
        switch (value)
        {
            case int intValue:
                writer.WriteNumber(name, intValue);
                break;
            case float floatValue:
                writer.WriteNumber(name, floatValue);
                break;
            case double doubleValue:
                writer.WriteNumber(name, doubleValue);
                break;
            case bool boolValue:
                writer.WriteBoolean(name, boolValue);
                break;
            case string stringValue:
                writer.WriteString(name, stringValue);
                break;
            default:
                throw new ArgumentException($"Type is not a primitive, type: {value.GetType()}");
        }
    }
    
    public static JsonSchema GetSchema(this JsonElement element)
    {
        var type = element.GetProperty("type").GetString();
        if (type == "object")
        {
            var objSchema = new JsonObjectSchema();
            
            var propertiesObj = element.GetProperty("properties");
            objSchema.Properties =  new Dictionary<string, JsonSchema>();
            foreach (var propertyObj in propertiesObj.EnumerateObject())
            {
                var propertyName = propertyObj.Name;
                var propertySchema = propertyObj.Value.GetSchema();
                objSchema.Properties.Add(propertyName, propertySchema);
            }

            if (element.TryGetProperty("required", out var requiredObj))
            {
                var requiredPropertiesCout = requiredObj.GetArrayLength();
                var requiredProperties = new string[requiredPropertiesCout];
                for (var i = 0; i < requiredPropertiesCout; i++)
                {
                    var requiredProperty = requiredObj[i].GetString();
                    requiredProperties[i] = requiredProperty!;
                }
                objSchema.Required = requiredProperties;
            }

            if (element.TryGetProperty("description", out var descriptionProp))
            {
                objSchema.Description = descriptionProp.GetString();
            }
            
            return objSchema;
        }
        
        if (type == "string")
        {
            var stringSchema = new JsonStringSchema();
         
            if (element.TryGetProperty("minLength", out var minLengthProp))
            {
                stringSchema.MinLength = minLengthProp.GetInt32();
            }
            
            if (element.TryGetProperty("maxLength", out var maxLengthProp))
            {
                stringSchema.MaxLength = maxLengthProp.GetInt32();
            }
            
            if (element.TryGetProperty("description", out var descriptionProp))
            {
                stringSchema.Description = descriptionProp.GetString();
            }

            return stringSchema;
        }
        
        if (type == "number")
        {
            var numberSchema = new JsonNumberSchema();
         
            if (element.TryGetProperty("maximum", out var maximumProp))
            {
                numberSchema.Maximum = maximumProp.GetInt32();
            }
            
            if (element.TryGetProperty("minimum", out var minimumProp))
            {
                numberSchema.Minimum = minimumProp.GetInt32();
            }
            
            if (element.TryGetProperty("description", out var descriptionProp))
            {
                numberSchema.Description = descriptionProp.GetString();
            }

            return numberSchema;
        }
        
        throw new Exception($"Unsupported type: {type}");
    }
}