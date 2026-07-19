namespace AdrTool.Commands;

/// <summary>Shared argument parsing and execution for <see cref="AcceptCommand"/> and <see cref="RejectCommand"/>.</summary>
internal static class StatusChangeCommand
{
    public static int Execute(string commandName, string status, string[] args, AdrService service)
    {
        if (args.Length < 1 || !int.TryParse(args[0].TrimStart('#'), out var number))
            throw new AdrToolException($"Usage: adr {commandName} <n>");

        var file = service.SetStatus(number, status);

        Console.WriteLine($"Marked {file} as {status}");
        return 0;
    }
}
