using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DocPilot.Configuration;

public sealed class ConfigurationLoader
{
    private const string DefaultConfigFileName = "docpilot.yml";

    private readonly IDeserializer _deserializer;

    public ConfigurationLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public DocPilotConfig Load(string? configPath = null)
    {
        var path = ResolveConfigPath(configPath);

        if (path is null || !File.Exists(path))
        {
            return DocPilotConfig.Default;
        }

        return LoadFromFile(path);
    }

    public async Task<DocPilotConfig> LoadAsync(string? configPath = null, CancellationToken cancellationToken = default)
    {
        var path = ResolveConfigPath(configPath);

        if (path is null || !File.Exists(path))
        {
            return DocPilotConfig.Default;
        }

        return await LoadFromFileAsync(path, cancellationToken);
    }

    private static string? ResolveConfigPath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        // Search up directory tree for config file
        var currentDir = Directory.GetCurrentDirectory();

        while (currentDir is not null)
        {
            var configFile = Path.Combine(currentDir, DefaultConfigFileName);
            if (File.Exists(configFile))
            {
                return configFile;
            }

            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        return null;
    }

    private DocPilotConfig LoadFromFile(string path)
    {
        try
        {
            var yaml = File.ReadAllText(path);
            return ParseYaml(yaml);
        }
        catch (Exception ex) when (ex is not IOException)
        {
            throw new ConfigurationException($"Failed to parse configuration file: {path}", ex);
        }
    }

    private async Task<DocPilotConfig> LoadFromFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var yaml = await File.ReadAllTextAsync(path, cancellationToken);
            return ParseYaml(yaml);
        }
        catch (Exception ex) when (ex is not IOException and not OperationCanceledException)
        {
            throw new ConfigurationException($"Failed to parse configuration file: {path}", ex);
        }
    }

    private DocPilotConfig ParseYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return DocPilotConfig.Default;
        }

        var config = _deserializer.Deserialize<DocPilotConfig>(yaml);
        return config ?? DocPilotConfig.Default;
    }
}

public sealed class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
