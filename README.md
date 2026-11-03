> **Disclaimer.** Dark Age of Camelot is the intellectual property of Electronic Arts and Broadsword
> Online Games. Offline DAoC True 1.65 is not affiliated with, endorsed by, or connected to either company. The
> game client files, textures, models and other assets it uses belong to them, and this project is built on
> top of that copyrighted work.
>
> This is a free, non-commercial fan project. It is not a product and it is not for sale. No donations
> are accepted, and the project owner has not made, and will not make, any money from it. The client
> files are the same publicly available ones every free shard uses; the work original to this
> repository is the bot system, the server customizations and the new client's code. Because the code is open
> source, the owner cannot control what forks or other people do with it.
>
> This "time capsule" of the 2003 game was never meant to compete with the current retail Dark Age
> of Camelot, which is a very different, modern experience. If that interests you, please visit the
> official site: [darkageofcamelot.com](https://www.darkageofcamelot.com/).

> [!IMPORTANT]
> **This is an AI-made project.** All of its code was written by AI tools (Codex, then Claude). The project owner
> has no game development or coding experience: they directed the work and play-tested it. Expect bugs.
>
> **v0.2 is out**, with an in-place update for v0.1 players. Large parts of the game haven't been played through by a person.
> Expect bugs, and keep a backup of your save. Bug reports are welcome in [Issues](https://github.com/shadowofze/OfflineDAoC-True165/issues).

# Offline DAoC True 1.65: single-player Dark Age of Camelot as of patch 1.65

Offline DAoC True 1.65 is a local, single-player Dark Age of Camelot that runs entirely on your own PC. It
recreates the game as it was at **patch 1.65** (Classic and Shrouded Isles, November 2003), and fills the world
with gamebots that level, group, trade, craft and fight on their own.

It grew out of [Offline DAoC](https://github.com/shadowofze/OfflineDAoC), which is now on hold. Everything this
project adds to the OpenDAoC server was written by AI tools (Codex, then Claude), as was the whole new client. The
project owner, who has no game development or coding experience, directed and play-tested it.

## Download and play

**[Download v0.2](https://github.com/shadowofze/OfflineDAoC-True165/releases/latest)**, no Git or AI tools needed.
**Already playing v0.1?** The same downloader updates your game in place with a much smaller download (about 2.5 GB). Your
account, characters, companions and bots stay as they are.

1. On the release page, under **Assets**, download **`Download-OfflineDAoC-True165-v0.2.zip`** (just the
   downloader, a few KB).
2. Put it in a folder with a short path, such as `C:\Games\OfflineDAoC`. Right-click it, choose **Extract All…**,
   and open the extracted folder.
3. Close the game and the launcher if they're running. Then double-click **`GET-OFFLINE-DAOC-TRUE165.cmd`**. If
   Windows SmartScreen appears, click **More info → Run anyway**. It checks your PC and asks:
   - **1 = Update my game:** it finds your `OfflineDAoC-True165` folder (or asks you to pick it), checks it, and
     updates it in place (about 2.5 GB);
   - **2 = New install:** the complete game, into a new folder (about 36 GB).

   If it stops, run it again; it continues where it stopped.
4. In the `OfflineDAoC-True165` folder, double-click **`START OFFLINE DAOC TRUE 1.65.cmd`**. Click **START
   SERVER**, wait for **RUNNING**, then click **ENTER REALM**. Your account is created the first time.

What you need:
- 64-bit Windows 10 or 11, with a CPU that supports **AVX2** (the bots' navigation needs it; most CPUs from 2014
  onward do).
- A graphics card that supports **Vulkan**, with current drivers. It's developed and tested on an RTX 2060 (6 GB).
- 16 GB RAM recommended, and about 48 GB of free disk space for a new install.
- **Nothing to install or turn on in Windows:** .NET comes with the game. You don't need the Windows ".NET Framework
  3.5" feature, the Visual C++ Redistributable or an original DAoC client.

The full walkthrough with troubleshooting is in [docs/PLAY.md](docs/PLAY.md), and the client's guide is in
[docs/NEW-CLIENT.md](docs/NEW-CLIENT.md). **Code > Download ZIP** on this page gives you the *source code*, not the
playable game.

**On a Mac?** There's no Mac download, but you can build it from this repository and run it with the Windows
release's content. See [docs/MACOS.md](docs/MACOS.md).

**Handy commands:** [QUICK COMMANDS.txt](QUICK%20COMMANDS.txt) has the everyday commands and the launcher
shortcuts: hiring and managing companions, `/task`, `/questguide`, the Horn of Rallying, horse routes and training.
Every command that works in True 1.65 is in [ALL SERVER COMMANDS.txt](ALL%20SERVER%20COMMANDS.txt), with the GM-only
ones marked. Both files are in your game folder too.

## Built on OpenDAoC and Dawn of Light

The server is a modified [OpenDAoC](https://github.com/OpenDAoC/OpenDAoC-Core), which itself grew from
[Dawn of Light](https://github.com/Dawn-of-Light/DOLSharp) (DOL), the long-running open-source DAoC server emulator.
Their combat, spell, item, quest and world systems are the foundation everything here is built on, and their GPL
license and credits carry over to this project. True 1.65 adds the bot systems, the hired companions and the 1.65
rules on top, and replaces content that freeshards added later with period data.

## Built from period data

The 1.65 world is rebuilt from **about 85,000 pages of period data**, collected from the public sources players
used at the time:

| Source | Pages | What it gave |
|---|---|---|
| Uthgard bestiary (a 1.65 recreation) | about 25,000 | monsters, items and measured drop rates from players' kill logs |
| Allakhazam, the current site | about 24,700 | monsters, items, quests and zones, with player comments |
| Allakhazam as it read in 2001-2004 (Wayback Machine) | about 17,000 | period item stats, monster loot, merchant stock and quest walkthroughs |
| Camelot Warcry (Wayback Machine, 2001-2004) | about 11,600 | quest spoilers, items and news |
| CapnBry's DAoC radar | about 6,000 | monster positions and levels from 176,970 live-server sightings |

The patch 1.64 client's own data files supplied the recipe list, the class and race tables and the pre-game
screens.

Period sources come first: an item, drop or spawn stays only when the period data supports it, and nothing is
invented unless no source exists. **The period data itself is not in this repository at this time.** What's here
is the result: the world database and the content files built from it.

## How it differs from Offline DAoC

| | Offline DAoC (0.37 and earlier) | Offline DAoC True 1.65 |
|---|---|---|
| **Rules** | OpenDAoC's freeshard ruleset, plus many custom additions | Patch 1.65 rules only, with no mode toggle |
| **Client** | The original DAoC client, and a beta new client in 0.37c | The new client only, built in Godot 4 |
| **Items and loot** | Freeshard items, random "ROG" drops, items with later stats | Period 1.65 items, drops and vendor stock only |
| **Leveling** | XP rate options, free levels, buff merchants, hasteners | XP fixed at 1x, with no shortcuts |
| **Getting around** | Teleporters and `/tele` | Walking, horse routes and the period portals |
| **Your group** | `/spawn` companion bots on demand | Hired companions that you level, spec and gear |
| **Bots** | Separate bot rules | The same rules as players |
| **Saves** | Progress transfers from every older version | A fresh start: no progress import from Offline DAoC |

## What True 1.65 has

**The 1.65 game:**
- The Classic and Shrouded Isles world, with the three islands added in 1.65 and the Darkness Falls and Shrouded
  Isles entrances where the 1.64 client puts them.
- Base classes at character creation: you choose your class when you promote at a trainer at level 5. You spend
  your own stat points, and choose your height, face and hair. The pre-game screens follow the 1.64 client.
- You begin in your race and class's period starting town, with a weapon and a training shield. There are 8
  character slots per realm.
- Crafting from the 1.64 client's recipe list, with the period success rates, hand tools and salvage.
- Gear wears and is repaired for coin, and arrows, throwing weapons and poisons are bought or crafted.
- Coin belongs to each character, deaths cost constitution, and songs need their own instrument type.
- Period tasks (`/task`) and housing. Quests, epic quests and epic armour are being brought to their 1.65
  versions.
- Loot comes from period sources: Allakhazam's period pages, CapnBry's radar sightings and Uthgard's measured
  drop rates. Nothing from Trials of Atlantis or later.

**Gamebots on the player rules:**
- Bots start as a base class and promote at level 5, like you.
- They sit to rest at player regeneration rates and walk to their loot bags.
- They buy their armour from vendors, repair their gear, visit healers, craft, and charm monsters only with a
  charm spell they've learned.
- They walk and ride horse routes; there are no teleporters and no boats.

**Hired companions** instead of summoned bots:
- Hire them at the traveler's inns near Caer Witrin, Connla and Huginfell.
- A new slot opens at levels 5, 10, 15, 20, 30, 40 and 45, so a full group of 8 is possible at level 45.
- You choose each companion's look, spec, realm abilities and gear.
- They level with you, and a healer revives them for free.

**Realm war:**
- Keep sieges and relic raids run on their own, and also start when you attack a keep.
- PvE realm events start only when a player sounds the Horn of Rallying at an inn.

**Kept on purpose, although they aren't from 1.65:**
- The gamebots and the bot systems.
- The Realm Exchange: an auction house where gamebots and players trade with each other. Bots list what they
  craft and loot, and buy from each other's listings and yours; you can list your own items and buy theirs. Prices
  follow what an item is worth, and unsold bot listings are repriced. Only crafted and spellcrafted gear, potions,
  poisons, period drops and Darkness Falls items can be listed. Its brokers are in a small building by each inn.
- Boss changes that let bots finish raids, and faster respawns for named and quest monsters.
- Quest markers, quest objectives and the quest guide (`/questguide`).
- Slash commands that skip the period game, such as `/tele` and `/tc`. They're kept for GM accounts only.

## For developers and AI assistants

- **Where to start:** [AGENTS.md](AGENTS.md) (also read by Claude as [CLAUDE.md](CLAUDE.md)) has the project rules
  and the layout.
- **Guides:**
  - [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md): building and testing the server and launcher, and how the True 1.65
    content reaches the world;
  - [source/new-client/README.md](source/new-client/README.md): the client and its content pipeline;
  - [docs/LLM-QUICKSTART.md](docs/LLM-QUICKSTART.md): customizing it with your own AI.
- **Where the code is:**
  - `development-source/server`: the game server, the True 1.65 rules (`GameServer/mode165`) and all bot AI.
  - `development-source/tools`: the launcher and the navigation mesh builder.
  - `tools/claude-version/true165` and `runtime/true165`: the generators and the True 1.65 world content they make.
  - `database`: the clean world database.
  - `source/new-client`: the new client (Godot 4 project and its content pipeline).
- **Cloning on Windows:** clone into a short folder, such as `C:\src\`. If Git reports "Filename too long", turn on
  long paths (`git config --global core.longpaths true`) and clone again.

## Good to know

- **AI-written code can have bugs.** Keep a backup of your save before installing a new build or mod. There's
  no guarantee against regressions.
- **Licensing:** this repository holds the server, launcher, tools and the new client's source code. It does
  not hold the original game client's source. The new client's content is converted from the original
  client's files and ships only in the release download. The server's license doesn't relicense
  third-party client assets or dependencies. See [THIRD_PARTY.md](THIRD_PARTY.md).
- **Local only:** the default setup is for one PC. Running a public multiplayer server would need its own
  security work.
- **Forking:** you don't need permission to fork this repository and make it your own. Keep the existing
  licenses and credits ([THIRD_PARTY.md](THIRD_PARTY.md)).

## Known issues

- Horse routes: crossing Prydwen Bridge, the horse can dip into the deck now and then.
- Housing is new in v0.2: it is checked offline but not yet played through on a live server.
- Low-level gamebots (levels 6-9) die a lot: they start with no armour and have to buy it.
- Gear wears at the player rate, so bots rarely need repairs early on. This is expected.
- About 2,100 monster names drop only coin, mostly in the Shrouded Isles, where no period source recorded their loot.
- About 133 low-confidence period merchant goods are left out, and the Darkness Falls seal stock still includes later
  items beside the period ones.
- Support bots don't leave combat while only their pet fights.
- The login panel is the older one, not the period 1.64 one.

## License

The server is licensed under the [GNU General Public License v3](LICENSE), like the OpenDAoC and Dawn of Light
code it is based on. Other parts keep their own licenses; see [THIRD_PARTY.md](THIRD_PARTY.md).

## Changes

See the [changelog](CHANGELOG.md), including what's coming in 0.2.

