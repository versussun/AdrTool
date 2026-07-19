using System.Text;

namespace AdrTool.Commands;

/// <summary>
/// "adr install-hooks [--dashboard-check] [--force]" — writes a git pre-commit hook that runs
/// "adr lint" (and, with "--dashboard-check", "adr dashboard --check") before allowing a commit.
/// Must be run from the repository root (where ".git" lives). Refuses to overwrite an existing
/// pre-commit hook unless "--force" is given.
/// </summary>
public sealed class InstallHooksCommand(string basePath) : ICommand
{
    public string Name => "install-hooks";

    public int Execute(string[] args)
    {
        var force = args.Contains("--force");
        var withDashboardCheck = args.Contains("--dashboard-check");

        var hooksDirectory = ResolveHooksDirectory(basePath);
        Directory.CreateDirectory(hooksDirectory);

        var hookPath = Path.Combine(hooksDirectory, "pre-commit");
        if (File.Exists(hookPath) && !force)
            throw new AdrToolException(
                $"{hookPath} already exists. Add the checks below manually, or re-run with --force to overwrite it.");

        var adrInvocation = File.Exists(Path.Combine(basePath, ".config", "dotnet-tools.json")) ? "dotnet adr" : "adr";

        var script = new StringBuilder()
            .AppendLine("#!/bin/sh")
            .AppendLine("# Installed by \"adr install-hooks\"")
            .AppendLine($"{adrInvocation} lint || exit 1");

        if (withDashboardCheck)
            script.AppendLine($"{adrInvocation} dashboard --check || exit 1");

        File.WriteAllText(hookPath, script.ToString());

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                hookPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        Console.WriteLine($"Installed pre-commit hook at {hookPath}");
        return 0;
    }

    /// <summary>
    /// Resolves the repo's hooks directory from a ".git" directory (normal clone) or a ".git" file
    /// pointing at "gitdir: &lt;path&gt;" (worktrees/submodules).
    /// </summary>
    private static string ResolveHooksDirectory(string basePath)
    {
        var gitPath = Path.Combine(basePath, ".git");

        if (Directory.Exists(gitPath))
            return Path.Combine(gitPath, "hooks");

        if (File.Exists(gitPath))
        {
            var gitDirLine = File.ReadAllLines(gitPath)
                .FirstOrDefault(line => line.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase));

            if (gitDirLine is not null)
            {
                var gitDir = gitDirLine["gitdir:".Length..].Trim();
                var resolved = Path.IsPathRooted(gitDir) ? gitDir : Path.GetFullPath(Path.Combine(basePath, gitDir));
                return Path.Combine(resolved, "hooks");
            }
        }

        throw new AdrToolException(
            $"No git repository found at {basePath} (expected a .git directory or file). Run \"adr install-hooks\" from the repo root.");
    }
}
