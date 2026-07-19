using AdrTool;
using AdrTool.Commands;

// The base path is the folder the tool is executed from.
var basePath = Directory.GetCurrentDirectory();
var config = AdrConfig.Load(basePath);
var service = new AdrService(basePath, config);

ICommand[] commands =
[
    new InitCommand(basePath),
    new NewCommand(service),
    new ListCommand(service),
    new ShowCommand(service),
    new EditCommand(service),
    new SearchCommand(service),
    new SupersedeCommand(service),
    new AcceptCommand(service),
    new RejectCommand(service),
    new LinkCommand(service),
    new TemplateCommand(config, basePath),
    new DashboardCommand(service),
    new LintCommand(service),
    new RenumberCommand(service),
    new InstallHooksCommand(basePath),
    new ConfigCommand(config, basePath),
    new CompletionCommand(),
    new HelpCommand(),
];

try
{
    return Run(args);
}
catch (AdrToolException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

int Run(string[] args)
{
    if (args.Length == 0)
    {
        HelpCommand.Print();
        return 1;
    }

    var commandName = args[0] is "-h" or "--help" ? "help" : args[0];
    var command = commands.FirstOrDefault(c => c.Name == commandName);

    if (command is null)
    {
        Console.Error.WriteLine($"Unknown command: {args[0]}");
        HelpCommand.Print();
        return 1;
    }

    return command.Execute(args[1..]);
}
