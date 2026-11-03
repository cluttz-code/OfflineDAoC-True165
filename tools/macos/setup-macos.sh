#!/bin/bash
# Sets up OfflineDAoC True 1.65 to run on macOS (Apple Silicon or Intel) from this repository. See docs/MACOS.md.
#
#   tools/macos/setup-macos.sh [--release <unpacked Windows release folder>] [--x64]
#
# It builds what the Windows release ships as Windows binaries:
#   1. the Detour pathfinding library  -> runtime/server/lib/Detour.dylib
#   2. the server (Release)            -> runtime/server (config/ is left as it is)
#   3. the command-line launcher       -> runtime/cli, with runtime/offlinedaoc to run it
#   4. the clean world database        -> runtime/data/opendaoc-165.sqlite3.db (only when there is none yet)
# With --release it also links the release download's platform-independent content into this tree: the full navigation
# meshes (runtime/server/navmesh) and the new client (runtime/new-client.txt points at its new-client folder).
# On Apple Silicon the SQLite package has no arm64 macOS library, so the server and the launcher run on an x64 .NET runtime
# under Rosetta. The script downloads that runtime to ~/.dotnet-x64 (or uses DOTNET_X64 / /usr/local/share/dotnet/x64)
# and installs Rosetta if needed. Everything is still built with your own .NET SDK. --x64 forces this on any Mac.
#
# Needs: Xcode command line tools (xcode-select --install), cmake, python3, and the .NET 10 SDK.
# It never starts the server and never replaces an existing world database.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
RUNTIME="$REPO/runtime"
SERVER_SRC="$REPO/development-source/server"
RELEASE=""
X64=0
while [ $# -gt 0 ]; do
    case "$1" in
        --release) RELEASE="${2:?--release needs a folder}"; shift 2 ;;
        --x64) X64=1; shift ;;
        -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
        *) echo "unknown option: $1" >&2; exit 2 ;;
    esac
done

say() { printf '\n==> %s\n' "$*"; }
die() { printf '\nERROR: %s\n' "$*" >&2; exit 1; }

# ---- tools ----------------------------------------------------------------------------------------------------------
# DOTNET builds; RUN_DOTNET runs the server and the launcher (the x64 one under Rosetta when SQLite needs it).
DOTNET="${DOTNET:-$(command -v dotnet || true)}"
[ -n "$DOTNET" ] && [ -x "$DOTNET" ] || die "dotnet not found. Install the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0"
"$DOTNET" --list-sdks | grep -q '^10\.' || die "The .NET 10 SDK is needed ($DOTNET --list-sdks shows none)."
command -v cmake >/dev/null || die "cmake not found (brew install cmake)."
command -v python3 >/dev/null || die "python3 not found."
command -v c++ >/dev/null || die "No C++ compiler: xcode-select --install"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

if pgrep -f 'CoreServer' >/dev/null 2>&1; then
    die "A server is running. Stop it first (type exit in its terminal)."
fi

# ---- 1. Detour ------------------------------------------------------------------------------------------------------
say "Building the Detour pathfinding library"
BUILD="$REPO/development-source/build-macos"
cmake -S "$SERVER_SRC/Pathing/Detour" -B "$BUILD/detour" -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" -DSOVERSION=1 -DLIB_VERSION=1.0.0 >/dev/null
cmake --build "$BUILD/detour" --config Release -j "$(sysctl -n hw.ncpu 2>/dev/null || echo 4)"
DYLIB="$(find "$BUILD/detour" -name 'libDetour*.dylib' -type f | head -1)"
[ -n "$DYLIB" ] || die "The Detour build made no libDetour*.dylib."
mkdir -p "$RUNTIME/server/lib"
# The server imports "lib/Detour"; .NET looks for lib/Detour.dylib relative to the server's folder.
cp -f "$DYLIB" "$RUNTIME/server/lib/Detour.dylib"
echo "    -> runtime/server/lib/Detour.dylib"

# ---- 2. the server --------------------------------------------------------------------------------------------------
say "Building the server (Release; takes a minute, only errors are shown)"
"$DOTNET" build "$SERVER_SRC/CoreServer/CoreServer.csproj" -c Release -v quiet -nologo -clp:ErrorsOnly
say "Copying the server build into runtime/server (config/ kept)"
rsync -a --exclude 'config/' "$SERVER_SRC/Release/" "$RUNTIME/server/"

RUN_DOTNET="$DOTNET"
if [ "$(uname -m)" = arm64 ] && \
   ! find "$RUNTIME/server/runtimes" -path '*osx-arm64*' -name 'SQLite.Interop*' 2>/dev/null | grep -q .; then
    X64=1
fi
if [ "$X64" = 1 ]; then
    say "Preparing the x64 .NET runtime (SQLite has no Apple Silicon library, so the server runs under Rosetta)"
    if ! arch -x86_64 /usr/bin/true 2>/dev/null; then
        echo "    Installing Rosetta (macOS may ask for your password)..."
        softwareupdate --install-rosetta --agree-to-license || \
            die "Rosetta could not be installed. Run: softwareupdate --install-rosetta --agree-to-license"
    fi
    RUN_DOTNET=""
    for candidate in "${DOTNET_X64:-}" /usr/local/share/dotnet/x64/dotnet "$HOME/.dotnet-x64/dotnet"; do
        if [ -n "$candidate" ] && [ -x "$candidate" ] && \
           "$candidate" --list-runtimes 2>/dev/null | grep -q '^Microsoft.AspNetCore.App 10\.'; then
            RUN_DOTNET="$candidate"; break
        fi
    done
    if [ -z "$RUN_DOTNET" ]; then
        echo "    Downloading the x64 .NET 10 runtime to ~/.dotnet-x64 (about 100 MB)..."
        INSTALLER="$BUILD/dotnet-install.sh"
        curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALLER" || die "Could not download dotnet-install.sh."
        for rt in dotnet aspnetcore; do
            bash "$INSTALLER" --channel 10.0 --runtime "$rt" --architecture x64 --install-dir "$HOME/.dotnet-x64" \
                --no-path >/dev/null || die "Could not install the x64 .NET runtime ($rt)."
        done
        RUN_DOTNET="$HOME/.dotnet-x64/dotnet"
    fi
    echo "    -> $RUN_DOTNET"
fi

# ---- 3. the command-line launcher -----------------------------------------------------------------------------------
say "Building the command-line launcher"
"$DOTNET" publish "$REPO/development-source/tools/OfflineDaoc.Cli/OfflineDaoc.Cli.csproj" -c Release \
    -o "$RUNTIME/cli" -p:UseAppHost=false -v quiet -nologo -clp:ErrorsOnly
cat > "$RUNTIME/offlinedaoc" <<EOF
#!/bin/bash
# OfflineDAoC True 1.65 command-line launcher (made by tools/macos/setup-macos.sh). offlinedaoc --help
export DOTNET="\${DOTNET:-$RUN_DOTNET}"
cd "\$(dirname "\$0")"
exec "\$DOTNET" "\$(pwd)/cli/offlinedaoc.dll" "\$@"
EOF
chmod +x "$RUNTIME/offlinedaoc"
echo "    -> runtime/offlinedaoc"

# ---- 4. the world ---------------------------------------------------------------------------------------------------
if [ -f "$RUNTIME/data/opendaoc-165.sqlite3.db" ]; then
    say "World database already present (kept): runtime/data/opendaoc-165.sqlite3.db"
else
    say "Unpacking the clean world database"
    python3 "$REPO/database/unpack_db.py"
fi

# ---- 5. content from the release download ---------------------------------------------------------------------------
if [ -n "$RELEASE" ]; then
    [ -d "$RELEASE" ] || die "The release folder '$RELEASE' does not exist. Pass the folder where you unzipped the
Windows release download (it holds runtime/ and new-client/). Tip: type --release and a space, then drag that folder
from Finder into the Terminal window to paste its path."
    RELEASE="$(cd "$RELEASE" && pwd)"
    say "Linking content from the release at $RELEASE"
    NAV="$RELEASE/runtime/server/navmesh"
    if [ -d "$NAV" ]; then
        if [ -e "$RUNTIME/server/navmesh" ] && [ ! -L "$RUNTIME/server/navmesh" ]; then
            echo "    runtime/server/navmesh exists already (kept)"
        else
            ln -sfn "$NAV" "$RUNTIME/server/navmesh"
            echo "    runtime/server/navmesh -> $NAV"
        fi
    else
        echo "    no runtime/server/navmesh in the release folder" >&2
    fi
    if [ -f "$RELEASE/new-client/godot/project.godot" ]; then
        printf '%s\n' "$RELEASE/new-client" > "$RUNTIME/new-client.txt"
        echo "    runtime/new-client.txt -> $RELEASE/new-client"
    else
        echo "    no new-client/godot/project.godot in the release folder" >&2
    fi
fi

# ---- what is still missing ------------------------------------------------------------------------------------------
say "Checks"
MISSING=0
if [ -d "$RUNTIME/server/navmesh" ]; then echo "    ok   navigation meshes"; else
    echo "    MISSING runtime/server/navmesh (2.1 GB, from the release download; use --release)"; MISSING=1; fi
CLIENT=""
[ -f "$RUNTIME/new-client.txt" ] && CLIENT="$(head -1 "$RUNTIME/new-client.txt")"
[ -z "$CLIENT" ] && [ -f "$RUNTIME/../new-client/godot/project.godot" ] && CLIENT="$RUNTIME/../new-client"
[ -z "$CLIENT" ] && CLIENT="$REPO/source/new-client"
if [ -d "$CLIENT/godot/content" ]; then echo "    ok   client content ($CLIENT)"; else
    echo "    MISSING the client's content ($CLIENT/godot/content, 37 GB, from the release download; use --release)"; MISSING=1; fi
if [ -f "$CLIENT/godot/.godot/mono/temp/bin/Debug/MMOEngine.dll" ]; then echo "    ok   client C# build"; else
    echo "    MISSING the client's C# build: see docs/MACOS.md (Building the client's C#)"; MISSING=1; fi
if [ -d /Applications/Godot_mono.app ] || [ -n "${GODOT:-}" ]; then echo "    ok   Godot .NET"; else
    echo "    MISSING Godot 4.7.2 .NET for macOS in /Applications/Godot_mono.app (or set GODOT)"; MISSING=1; fi

cat <<EOF

Done.$([ "$MISSING" = 1 ] && echo " Fill in what is MISSING above first.")
  Terminal 1:  $RUNTIME/offlinedaoc start     (type exit or press Ctrl+C to stop and save)
  Terminal 2:  $RUNTIME/offlinedaoc client
  Bots:        $RUNTIME/offlinedaoc add-bots <realm 1-3> <count> <level 1|50>   (with the server stopped)
EOF
