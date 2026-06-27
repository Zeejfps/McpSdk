using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests;

/// <summary>
/// A tool that declares an <c>outputSchema</c> and returns structured output (Phase C). Adds two
/// numbers and reports the sum both as <c>structuredContent</c> and, via
/// <see cref="CallToolResult.Structured"/>, as a back-compat serialized-JSON text block.
/// </summary>
public sealed class StructuredToolHandler : IToolHandler
{
    private readonly IJson _json;

    public Tool Tool { get; }

    public StructuredToolHandler(IJson json)
    {
        _json = json;
        Tool = new Tool(
            "add",
            "Adds two numbers and returns their sum.",
            new ObjectSchema
            {
                { "a", new NumberSchema { Description = "First addend" } },
                { "b", new NumberSchema { Description = "Second addend" } },
            })
        {
            OutputSchema = new ObjectSchema
            {
                { "sum", new NumberSchema { Description = "The sum of a and b" } },
            },
        };
    }

    public Task<CallToolResult> Call(IJsonObject args)
    {
        var a = args["a"].AsDouble();
        var b = args["b"].AsDouble();
        var structured = _json.Object(w => w.Write("sum", a + b));
        return Task.FromResult(CallToolResult.Structured(structured));
    }
}
