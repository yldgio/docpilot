using System.ComponentModel;
using DocPilot.Generation;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static class GenerateDocPatchTool
{
    public static AIFunction Create(AgentContext context)
    {
        return AIFunctionFactory.Create(
            ([Description("Target file path for the documentation")] string filePath,
             [Description("Operation: create, update, or append")] string operation,
             [Description("The documentation content in markdown format")] string content,
             [Description("Optional section header to append to")] string? section = null) =>
            {
                var op = operation.ToLowerInvariant() switch
                {
                    "create" => PatchOperation.Create,
                    "update" => PatchOperation.Update,
                    "append" => PatchOperation.Append,
                    "delete" => PatchOperation.Delete,
                    _ => PatchOperation.Update
                };

                var patch = new DocPatch
                {
                    FilePath = filePath,
                    Operation = op,
                    Content = content,
                    Section = section,
                    SourceReferences = context.MappingResult?.Targets
                        .FirstOrDefault(t => t.FilePath == filePath)?.SourceFiles ?? [],
                    Confidence = context.MappingResult?.AverageConfidence ?? 0.5
                };

                context.GeneratedPatches.Add(filePath);

                return new
                {
                    success = true,
                    filePath = patch.FilePath,
                    operation = patch.Operation.ToString(),
                    contentLength = patch.Content.Length,
                    hasSection = patch.Section is not null
                };
            },
            "generate_doc_patch",
            "Generate a documentation patch for a specific file");
    }
}
