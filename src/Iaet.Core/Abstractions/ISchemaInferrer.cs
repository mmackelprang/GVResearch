namespace Iaet.Core.Abstractions;

public interface ISchemaInferrer
{
    Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default);
}

public sealed record SchemaResult(
    string JsonSchema,
    string CSharpRecord,
    string OpenApiFragment,
    IReadOnlyList<string> Warnings
);
