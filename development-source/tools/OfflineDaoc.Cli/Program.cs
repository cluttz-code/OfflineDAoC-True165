using System.Diagnostics;
using System.Text;

namespace OfflineDaoc.Launcher;

/// <summary>
/// offlinedaoc: the launcher's work from a terminal, for macOS and Linux (the Windows launcher is WinForms).
///   offlinedaoc start                         content refresh, then the server in this terminal (type exit to stop)
///   offlinedaoc refresh                       the content refresh only (as OfflineDAoC.exe --refresh-165)
///   offlinedaoc add-bots realm count level    world gamebots (as the ADD BOTS buttons / --add-bots 1.65)
///   offlinedaoc client                        the new client, logged in with account.txt (as ENTER REALM)
///   offlinedaoc account                       prints the local login (makes one on first use)
/// The runtime folder (the one holding true165/, server/ and data/) is found from --root, OFFLINEDAOC_RUNTIME, or by
/// looking up from the current folder and from this program's folder.
/// </summary>
internal static class Program
{
    private const int ServerPort = 10300;

    private static int Main(string[] args)
    {
        var rest = new List<string>(args);
        string? root = TakeOption(rest, "--root") ?? Environment.GetEnvironmentVariable("OFFLINEDAOC_RUNTIME");
        string? godot = TakeOption(rest, "--godot") ?? Environment.GetEnvironmentVariable("GODOT");
        string? clientFolder = TakeOption(rest, "--client") ?? Environment.GetEnvironmentVariable("OFFLINEDAOC_CLIENT");
        if (rest.Count == 0 || rest[0] is "-h" or "--help" or "help")
        {
            Usage();
            return rest.Count == 0 ? 1 : 0;
        }
        try
        {
            root = FindRuntime(root);
            string command = rest[0].ToLowerInvariant();
            return command switch
            {
                "start" => Start(root, rest.Contains("--no-refresh")),
                "refresh" => Refresh(root) ? 0 : 1,
                "add-bots" => AddBots(root, rest.Skip(1).ToArray()),
                "client" => Client(root, godot, clientFolder),
                "account" => PrintAccount(root),
                _ => throw new ArgumentException($"unknown command '{rest[0]}' (offlinedaoc --help)"),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("offlinedaoc: " + ex.Message);
            return 1;
        }
    }

    private static void Usage() => Console.WriteLine("""
        offlinedaoc - OfflineDAoC True 1.65 from a terminal (macOS / Linux)

          offlinedaoc start [--no-refresh]          refresh the True 1.65 content, then run the server here
                                                    (type exit, or press Ctrl+C, to stop it and save)
          offlinedaoc refresh                       the content refresh only
          offlinedaoc add-bots <realm> <count> <level>
                                                    add world gamebots: realm 1 Albion, 2 Midgard, 3 Hibernia;
                                                    count 1, 10, 100 (or any number); level 1 or 50
          offlinedaoc client [--godot <Godot>] [--client <new-client folder>]
                                                    start the new client logged in to the local server
          offlinedaoc account                       show the local login (account.txt)

        Options: --root <runtime folder> (or OFFLINEDAOC_RUNTIME), --godot (or GODOT), --client (or OFFLINEDAOC_CLIENT).
        Stop the server before refresh and add-bots.
        """);

    private static string? TakeOption(List<string> args, string name)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].StartsWith(name + "=", StringComparison.Ordinal))
            {
                string value = args[i][(name.Length + 1)..];
                args.RemoveAt(i);
                return value;
            }
            if (args[i] == name && i + 1 < args.Count)
            {
                string value = args[i + 1];
                args.RemoveRange(i, 2);
                return value;
            }
        }
        return null;
    }

    private static bool IsRuntime(string folder) =>
        Directory.Exists(Path.Combine(folder, "true165")) && Directory.Exists(Path.Combine(folder, "server"));

    private static string FindRuntime(string? given)
    {
        if (!string.IsNullOrWhiteSpace(given))
        {
            string full = Path.GetFullPath(given);
            if (!IsRuntime(full))
                throw new DirectoryNotFoundException($"{full} is not a runtime folder (it needs true165/ and server/).");
            return full;
        }
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (DirectoryInfo? dir = new(start); dir != null; dir = dir.Parent)
            {
                if (IsRuntime(dir.FullName))
                    return dir.FullName;
                string child = Path.Combine(dir.FullName, "runtime");
                if (IsRuntime(child))
                    return child;
            }
        }
        throw new DirectoryNotFoundException("No runtime folder found. Run from inside it, or pass --root <runtime folder>.");
    }

    /// <summary>Whether the server accepts connections on 127.0.0.1:10300 (a real connection attempt: the listener table
    /// that IPGlobalProperties reads is not reliable on macOS).</summary>
    private static bool ServerListening()
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            return client.ConnectAsync("127.0.0.1", ServerPort).Wait(500) && client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void RequireServerStopped()
    {
        if (ServerListening() || Process.GetProcessesByName("CoreServer").Length > 0)
            throw new InvalidOperationException("the server is running; stop it first (type exit in its terminal).");
    }

    private static void Log(string file, string message) =>
        File.AppendAllText(file, $"{DateTime.Now:s} {message}{Environment.NewLine}");

    private static bool Refresh(string root)
    {
        RequireServerStopped();
        string log = Path.Combine(root, "data", "true165-refresh.log");
        TrueMode.Mode mode = TrueMode.Read(root);
        if (!TrueMode.DatabaseAgrees(mode, out string problem))
        {
            Console.Error.WriteLine(problem + " (python3 database/unpack_db.py writes the clean world)");
            return false;
        }
        try
        {
            bool changed = TrueMode.Refresh(root, message =>
            {
                Console.WriteLine(message);
                Log(log, message);
            });
            string result = changed ? "updated" : "already current";
            Log(log, result);
            Console.WriteLine($"{TrueMode.Name} content: {result}.");
            return true;
        }
        catch (Exception ex)
        {
            Log(log, "FAILED: " + ex.Message);
            Console.Error.WriteLine($"The {TrueMode.Name} content update failed; the database is unchanged.\n{ex.Message}");
            return false;
        }
    }

    private static int AddBots(string root, string[] args)
    {
        if (args.Length < 3)
            throw new ArgumentException("usage: offlinedaoc add-bots <realm 1-3> <count> <level 1|50>");
        RequireServerStopped();
        int realm = int.Parse(args[0]), count = int.Parse(args[1]), level = int.Parse(args[2]);
        if (realm is < 1 or > 3) throw new ArgumentException("realm is 1 (Albion), 2 (Midgard) or 3 (Hibernia)");
        if (count < 1) throw new ArgumentException("count must be at least 1");
        if (level is not (1 or 50)) throw new ArgumentException("level is 1 or 50");
        TrueMode.Mode mode = TrueMode.Read(root);
        if (!TrueMode.DatabaseAgrees(mode, out string problem))
            throw new InvalidOperationException(problem);
        int left = count;
        while (left > 0)
        {
            int batch = left >= 100 ? 100 : left >= 10 ? 10 : 1;
            BotRoster.GenerateBotCharacters(mode.DatabasePath, true, realm, batch, level);
            left -= batch;
        }
        string message = $"added {count} level-{level} bots, realm {realm}, 1.65";
        Log(Path.Combine(root, "data", "add-bots.log"), message);
        Console.WriteLine(message);
        return 0;
    }

    /// <summary>START SERVER: the refresh, then CoreServer in this terminal. Its console is copied to
    /// logs/server-console.log; lines typed here go to the server, and Ctrl+C sends it "exit" (a clean, saved stop).</summary>
    private static int Start(string root, bool skipRefresh)
    {
        RequireServerStopped();
        if (!skipRefresh && !Refresh(root))
            return 1;
        string serverDir = Path.Combine(root, "server");
        string config = "./" + TrueMode.Config165.Replace('\\', '/');
        if (!File.Exists(Path.Combine(serverDir, config)))
            throw new FileNotFoundException($"{Path.Combine(serverDir, config)} is missing.");

        // CoreServer.dll through dotnet: the one in DOTNET when set (e.g. the x64 .NET under Rosetta), else the one on PATH.
        string dll = Path.Combine(serverDir, "CoreServer.dll");
        if (!File.Exists(dll))
            throw new FileNotFoundException($"No server build in {serverDir} (CoreServer.dll). Run the setup script first.");
        string dotnet = Environment.GetEnvironmentVariable("DOTNET") is { Length: > 0 } custom ? custom : "dotnet";
        string command = $"{Quote(dotnet)} {Quote(dll)} --start {Quote("-config=" + config)}";

        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        using var consoleLog = new StreamWriter(Path.Combine(logs, "server-console.log"), append: true, new UTF8Encoding(false))
            { AutoFlush = true };

        // The terminal sends Ctrl+C to every process in it. The server starts with SIGINT ignored (an ignored signal stays
        // ignored for a .NET program), so only this launcher sees it and asks the server to stop with "exit".
        var start = new ProcessStartInfo("/bin/sh")
        {
            WorkingDirectory = serverDir,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            ArgumentList = { "-c", "trap '' INT; exec " + command },
        };
        using var server = new Process { StartInfo = start };
        server.OutputDataReceived += (_, e) => { if (e.Data != null) { Console.WriteLine(e.Data); WriteLog(consoleLog, e.Data); } };
        server.ErrorDataReceived += (_, e) => { if (e.Data != null) { Console.Error.WriteLine(e.Data); WriteLog(consoleLog, "ERROR: " + e.Data); } };
        Console.WriteLine($"Starting the server in {serverDir} ... (type exit or press Ctrl+C to stop it)");
        server.Start();
        server.BeginOutputReadLine();
        server.BeginErrorReadLine();

        bool exitSent = false;
        void SendExit()
        {
            if (exitSent || server.HasExited) return;
            exitSent = true;
            Console.WriteLine("Stopping the server (exit) ...");
            try { server.StandardInput.WriteLine("exit"); server.StandardInput.Flush(); } catch (Exception) { }
        }
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; SendExit(); };

        var input = new Thread(() =>
        {
            while (!server.HasExited)
            {
                string? line = Console.ReadLine();
                if (line == null) { Thread.Sleep(500); continue; }
                if (line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) { SendExit(); continue; }
                try { server.StandardInput.WriteLine(line); server.StandardInput.Flush(); } catch (Exception) { break; }
            }
        }) { IsBackground = true };
        input.Start();

        var ready = new Thread(() =>
        {
            while (!server.HasExited)
            {
                if (ServerListening())
                {
                    Console.WriteLine($"*** The server is ready on 127.0.0.1:{ServerPort}. In another terminal: offlinedaoc client");
                    return;
                }
                Thread.Sleep(1000);
            }
        }) { IsBackground = true };
        ready.Start();

        server.WaitForExit();
        Console.WriteLine($"The server stopped (exit code {server.ExitCode}).");
        return server.ExitCode;
    }

    private static void WriteLog(StreamWriter log, string line)
    {
        lock (log)
        {
            try { log.WriteLine($"{DateTime.Now:s} {line}"); } catch (Exception) { }
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    /// <summary>The launcher's account.txt (Account: / Password: lines), made once with a random login if missing; the
    /// server's automatic account creation makes the account at the first login.</summary>
    private static (string Account, string Password) Credentials(string root)
    {
        string path = Path.Combine(root, "account.txt");
        if (!File.Exists(path))
        {
            const string letters = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            string password = new(Enumerable.Range(0, 16)
                .Select(_ => letters[System.Security.Cryptography.RandomNumberGenerator.GetInt32(letters.Length)]).ToArray());
            string account = "player" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000);
            File.WriteAllText(path, $"Account: {account}{Environment.NewLine}Password: {password}{Environment.NewLine}");
        }
        var values = File.ReadLines(path)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        return (values["Account"], values["Password"]);
    }

    private static int PrintAccount(string root)
    {
        var (account, password) = Credentials(root);
        Console.WriteLine($"Account:  {account}\nPassword: {password}\n(from {Path.Combine(root, "account.txt")})");
        return 0;
    }

    /// <summary>ENTER REALM: the new client, logged in to the local server with the launcher's account.</summary>
    private static int Client(string root, string? godot, string? clientFolder)
    {
        string folder = FindClient(root, clientFolder);
        string runner = FindGodot(folder, godot);
        if (!ServerListening())
            Console.WriteLine($"Note: nothing is listening on port {ServerPort} yet; start the server first (offlinedaoc start).");
        var (account, password) = Credentials(root);
        var start = new ProcessStartInfo(runner)
        {
            WorkingDirectory = folder,
            UseShellExecute = false,
            ArgumentList = { "--path", Path.Combine(folder, "godot"), "--", "--server=127.0.0.1", $"--account={account}",
                $"--password={password}", "--world-mode=1.65" },
        };
        // As the Windows launcher: the game's saves inside its folder (Godot reads APPDATA on Windows only; on macOS its
        // user data is in ~/Library/Application Support/Godot/app_userdata).
        start.Environment["APPDATA"] = Path.Combine(folder, "save");
        string logs = OperatingSystem.IsMacOS()
            ? "~/Library/Application Support/Godot/app_userdata/MMOEngine/logs/godot.log"
            : "~/.local/share/godot/app_userdata/MMOEngine/logs/godot.log";
        Console.WriteLine($"Starting the new client: {runner}");
        Console.WriteLine($"(Its messages show here and in {logs}. Closing the game window ends this.)");
        using Process? process = Process.Start(start);
        if (process == null)
            return 1;
        process.WaitForExit();
        Console.WriteLine($"The client closed (exit code {process.ExitCode}).");
        return process.ExitCode;
    }

    private static string FindClient(string root, string? given)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(given))
            candidates.Add(Path.GetFullPath(given));
        string pointer = Path.Combine(root, "new-client.txt");
        if (File.Exists(pointer))
        {
            string line = File.ReadLines(pointer).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#')) ?? string.Empty;
            if (line.Length > 0)
                candidates.Add(Path.GetFullPath(Path.IsPathRooted(line) ? line : Path.Combine(root, line)));
        }
        candidates.Add(Path.GetFullPath(Path.Combine(root, "..", "new-client")));               // the release layout
        candidates.Add(Path.GetFullPath(Path.Combine(root, "..", "source", "new-client")));     // the repository
        foreach (string folder in candidates)
            if (File.Exists(Path.Combine(folder, "godot", "project.godot")))
                return folder;
        throw new DirectoryNotFoundException("The new client was not found. Pass --client <folder holding godot/project.godot>.");
    }

    private static string FindGodot(string clientFolder, string? given)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(given))
            candidates.Add(given);
        string tools = Path.Combine(clientFolder, "tools", "godot_net");
        if (Directory.Exists(tools))
            candidates.AddRange(Directory.EnumerateDirectories(tools, "*.app").OrderByDescending(f => f));
        candidates.Add("/Applications/Godot_mono.app");
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Godot_mono.app"));
        foreach (string candidate in candidates)
        {
            // a macOS app bundle: its executable is Contents/MacOS/Godot
            if (candidate.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(candidate))
            {
                string macos = Path.Combine(candidate, "Contents", "MacOS");
                string? exe = Directory.Exists(macos) ? Directory.EnumerateFiles(macos).FirstOrDefault() : null;
                if (exe != null)
                    return exe;
            }
            else if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("Godot 4.7.2 (.NET) was not found. Install Godot_mono.app in /Applications, or pass " +
                                        "--godot <path to Godot_mono.app or its executable> (or set GODOT).");
    }
}
