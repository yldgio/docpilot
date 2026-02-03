using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace DocPilot.Agents.Tools;

public static class ReadFileTool
{
    public static AIFunction Create(AgentContext context)
    {
        Func<string, object> handler = ([Description("Relative path to the file to read")] string filePath) =>
        {
            var fullPath = Path.Combine(context.RepositoryPath, filePath);

            if (!File.Exists(fullPath))
            {
                return new { exists = false, content = (string?)null, error = (string?)null };
            }

            // Security check: ensure path is within repository
            var normalizedRepo = Path.GetFullPath(context.RepositoryPath);
            var normalizedFile = Path.GetFullPath(fullPath);

            if (!normalizedFile.StartsWith(normalizedRepo))
            {
                return new { exists = false, content = (string?)null, error = "Access denied: path outside repository" };
            }

            var content = File.ReadAllText(fullPath);

            // Truncate if too large
            if (content.Length > 10000)
            {
                content = content[..10000] + "\n\n[Content truncated...]";
            }

            return new
            {
                exists = true,
                content,
                error = (string?)null
            };
        };

        return AIFunctionFactory.Create(
            handler,
            "read_file",
            "Read the content of a file in the repository for context");
    }
}
