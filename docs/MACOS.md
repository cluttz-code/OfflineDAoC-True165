# Running OfflineDAoC True 1.65 on macOS

The game has three parts: the server, the launcher and the client. On macOS each one runs like this:

| Part | Windows | macOS |
|---|---|---|
| Server | `CoreServer.exe` (release download) | Built from this repository. It is the same .NET 10 code. |
| Pathfinding (Detour) | `lib/Detour.dll` | `lib/Detour.dylib`, built from `development-source/server/Pathing/Detour` |
| Launcher | `OfflineDAoC.exe` (WinForms) | `runtime/offlinedaoc`, a terminal launcher (`development-source/tools/OfflineDaoc.Cli`) |
| Client | Godot 4.7.2 .NET for Windows | Godot 4.7.2 .NET for macOS (`Godot_mono.app`) |
| Navmeshes, client content, world | release download / repository | the same files; they are not tied to one platform |

The terminal launcher does the Windows launcher's jobs:
- START SERVER, with the True 1.65 content refresh;
- `--refresh-165`;
- ADD BOTS;
- ENTER REALM.

It uses the same code (`TrueMode.cs`, `BotRoster.cs`, `BotCharacterGenerator.cs`), so it changes the world exactly as
the Windows launcher does. It has no population dashboard, bot list or settings panels.

## What you need

- macOS on Apple Silicon or Intel.
- Xcode command line tools: `xcode-select --install`.
- `cmake` and `python3`, for example `brew install cmake python`.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) for macOS.
- [Godot 4.7.2 stable, .NET build, for macOS](https://godotengine.org/download/archive/4.7.2-stable/). Put
  `Godot_mono.app` in `/Applications`, or set `GODOT` to where it is.
- The Windows release download, unpacked anywhere. It holds the navigation meshes (2.1 GB) and the client with its
  content (37 GB). These files work on any platform; only its `.exe` files don't.
- About 45 GB of free disk space and 8 GB or more of memory.

## Setup

```bash
git clone https://github.com/cluttz-code/OfflineDAoC-True165.git
```

```bash
cd OfflineDAoC-True165
```

```bash
tools/macos/setup-macos.sh --release "/path/to/unpacked/OfflineDAoC True 1.65"
```

The script:
1. builds `Detour.dylib` as a universal binary (arm64 and x86_64);
2. builds the server and copies it into `runtime/server` (it leaves `runtime/server/config` as it is);
3. builds the terminal launcher;
4. unpacks the clean world, but only if there is no world yet;
5. links the release's `runtime/server/navmesh` and `new-client` folders into this tree.

At the end it lists anything that's still missing. It never starts the server and never replaces a world database.

**Apple Silicon and SQLite.** The server reads the world through `System.Data.SQLite`. If that package has no native
arm64 macOS library, the script warns you. To fix it, run the server as x64 under Rosetta:

```bash
softwareupdate --install-rosetta
```

Then install the **x64** .NET 10 SDK for macOS (it goes to `/usr/local/share/dotnet/x64`) and run the script again
with `--x64`.

## Playing

Start the server in one terminal:

```bash
runtime/offlinedaoc start
```

Wait for `*** The server is ready`. The first start loads the world and the navigation meshes, so it takes a while.
Then start the client in a second terminal:

```bash
runtime/offlinedaoc client
```

The client logs in with the account in `runtime/account.txt`. The launcher makes that file the first time you use it,
and the server creates the account at your first login. `runtime/offlinedaoc account` shows the login.

To stop the server, type `exit` in its terminal or press Ctrl+C. Both send the server `exit`, so it saves and shuts
down cleanly. Don't close the terminal window while the server is running.

To add world gamebots, stop the server first. The realms are 1 Albion, 2 Midgard and 3 Hibernia; the level is 1 or 50.
For example, ten level-1 Albion bots:

```bash
runtime/offlinedaoc add-bots 1 10 1
```

Other commands:
- `runtime/offlinedaoc refresh` runs only the content refresh.
- `runtime/offlinedaoc --help` lists every command.

Logs:
- the server console: `runtime/logs/server-console.log`;
- the content refresh: `runtime/data/true165-refresh.log`;
- the client: Godot's user folder, `~/Library/Application Support/Godot/app_userdata/<project>/`.

## Building the client's C#

The release's `new-client` folder already has the built network client
(`godot/.godot/mono/temp/bin/Debug/MMOEngine.dll`). That is a plain .NET assembly, so macOS Godot runs it as it is.
To run the client from this repository instead, copy `godot/content` from the release into `source/new-client/godot/`.
Then build the C# against the macOS Godot's packages:

```bash
cd source/new-client/godot
```

```bash
dotnet build MMOEngine.csproj -c Debug --source /Applications/Godot_mono.app/Contents/Resources/GodotSharp/Tools/nupkgs
```

Run `runtime/offlinedaoc client --client source/new-client`, or leave `runtime/new-client.txt` out and the launcher
finds `source/new-client` itself.

## Known differences and limits

- The Windows launcher's dashboards aren't there: the bot list, RvR, realm events, the bot goal settings, the keep and
  relic reset and the Exchange sales. The server and the in-game commands work as on Windows.
- `/mobnav` travel input (`LocalPlayerTravelInput`) is Windows-only. It sends key presses to the original client's
  window, and the server already turns it off on other platforms.
- The server's performance counters (CPU, page faults, disk) show only on Windows and Linux.
- The client renders through Godot's Metal driver (Godot's default on macOS). It hasn't been tuned on Macs, so
  lower the graphics settings if the frame rate is low.
