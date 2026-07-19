using System.Diagnostics;

namespace AdrTool.Commands;

/// <summary>
/// "adr edit &lt;n&gt;" — opens the ADR's file in $VISUAL or $EDITOR (falling back to a platform
/// default), waits for the editor to exit, and returns its exit code.
/// </summary>
public sealed class EditCommand(AdrService service) : ICommand
{
    public string Name => "edit";

    public int Execute(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0].TrimStart('#'), out var number))
            throw new AdrToolException("Usage: adr edit <n>");

        var filePath = service.GetFilePath(number);

        var editorCommand = Environment.GetEnvironmentVariable("VISUAL")
            ?? Environment.GetEnvironmentVariable("EDITOR")
            ?? (OperatingSystem.IsWindows() ? "notepad" : "vi");

        var parts = editorCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new AdrToolException("$VISUAL/$EDITOR is set but empty.");

        var startInfo = new ProcessStartInfo
        {
            FileName = parts[0],
            UseShellExecute = false,
        };
        foreach (var arg in parts[1..])
            startInfo.ArgumentList.Add(arg);
        startInfo.ArgumentList.Add(filePath);

        using var process = Process.Start(startInfo)
            ?? throw new AdrToolException($"Failed to launch editor: {editorCommand}");
        process.WaitForExit();

        return process.ExitCode;
    }
}
