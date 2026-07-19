using AdrTool;
using AdrTool.Commands;

// The base path is the folder the tool is executed from.
var basePath = Directory.GetCurrentDirectory();

ICommand[] commands =
[
    new NewCommand(),
    new ListCommand(),
    new SupersedeCommand(),
    new TemplateCommand(),
    new DashboardCommand(),
    new HelpCommand(),
];

try
{
    return Run(args, basePath);
}
catch (AdrToolException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

int Run(string[] args, string basePath)
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

    return command.Execute(args[1..], basePath);
}
