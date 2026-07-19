namespace AdrTool.Commands;

/// <summary>
/// "adr init" — bootstraps a repo for ADRs: writes adr.config.json, creates the ADR directory,
/// and creates the standard "Record architecture decisions" meta-ADR as ADR 1.
/// </summary>
public sealed class InitCommand(string basePath) : ICommand
{
    public string Name => "init";

    private const string DefaultAdrPath = "docs/adr";

    public int Execute(string[] args)
    {
        var configPath = Path.Combine(basePath, AdrConfig.FileName);
        if (File.Exists(configPath))
            throw new AdrToolException($"{AdrConfig.FileName} already exists; this repository is already initialized.");

        var config = new AdrConfig { Path = DefaultAdrPath };
        config.Save(basePath);
        Console.WriteLine($"Created {configPath}");

        var service = new AdrService(basePath, config);
        var created = service.CreateInitialMetaAdr();
        Console.WriteLine($"Created {created}");

        return 0;
    }
}
