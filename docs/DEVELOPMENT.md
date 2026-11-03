# Developing Offline DAoC True 1.65

How to build, test and run the server and launcher, how the True 1.65 content reaches the world, and how a release
is put together. The new client has its own guide: [source/new-client/README.md](../source/new-client/README.md).
Read the project rules in [AGENTS.md](../AGENTS.md) (section 5 below repeats them in full) before you change
anything.

| Part | Where | What |
|---|---|---|
| Server | `development-source/server` | OpenDAoC fork ("Dawn of Light" solution), C#, `net10.0`. |
| Launcher | `development-source/tools/OfflineDaoc.Launcher` | WinForms app `OfflineDAoC.exe` (`net10.0-windows`). It starts the server and the client. |
| Tools | `development-source/tools` | The navmesh builder (`OpenDAoC-BuildNav`) and other development tools. |
| True 1.65 generators | `tools/claude-version/true165` | Python scripts that build the True 1.65 content files. |
| True 1.65 content | `runtime/true165` | The JSON files the launcher writes into the world database. |
| Clean world | `database/` | The clean world database (zipped) and the scripts that make and unpack it. |
| New client | `source/new-client` | The Godot client. See its README. |

## 0. A working setup from a clone

The repository holds the source and the content, not the binaries or the large generated files.

**Cloning on Windows:** clone into a short folder, such as `C:\src\`. If Git reports "Filename too long", turn on
long paths (`git config --global core.longpaths true`) and clone again.

To get a game you can run from your clone:

1. **World:** `python database/unpack_db.py` writes `runtime/data/opendaoc-165.sqlite3.db` (the clean world).
2. **Server and launcher:** build them (section 1), then copy the outputs into `runtime/server` (with `lib/` and
   `win-x64/`) and `runtime/`.
3. **Navigation meshes:** the full set (`runtime/server/navmesh`, 2.1 GB) comes from the release download. You can
   also rebuild it with `development-source/tools/OpenDAoC-BuildNav` from a DAoC client's zone data. The True 1.65
   inn zones' meshes are already in `runtime/server/navmesh-165`.
4. **Client:** the new client's content (about 37 GB) comes from the release download, or from its pipeline. See its
   README.
5. **.NET 10:** the runtimes `Microsoft.NETCore.App`, `Microsoft.WindowsDesktop.App` and `Microsoft.AspNetCore.App`,
   installed or bundled (section 7).

The easiest start is a playable release folder: work in a copy of it and deploy your builds there.

Not in the repository, and needed only by some tools:
- **the patch 1.64 DAoC client's data files** (zones.dat, the recipe list, figure tables): read by some generators
  and the client pipeline. They are the game's copyrighted files. Point the tools at your own copy.
- **the raw period data** (about 85,000 archived pages from Allakhazam, Uthgard, Warcry and CapnBry): read by the
  generators that rebuild drops, stats, spawns and merchants. It is not published at this time. The content files
  those generators made are in the repository, so you only need the archive to regenerate them.

---

## 1. Build and test

You need the .NET 10 SDK (the dev builds use 10.0.11).

```
cd development-source/server
dotnet build "Dawn of Light.sln" -c Release
dotnet test Tests/Tests.csproj -c Release --no-build
```

- The build output goes to `development-source/server/Release/` (`Release/lib/GameServer.dll`, `CoreDatabase.dll` and the rest).
- The test suite is NUnit, about 2,900 tests. A single test: `--filter "FullyQualifiedName~UT_ClassName"`.
- **Deploying a server build** means copying `Release/lib/GameServer.dll` and `Release/lib/CoreDatabase.dll` into
  all three runtime copies: `runtime/server/`, `runtime/server/lib/` and `runtime/server/win-x64/`. Back up what
  you replace. Only do this with the server stopped.
- `CLAUDE.md` and `AGENTS.md` in `development-source/server` still say .NET 9 in places. Every project now targets `net10.0`.

**Launcher** (its own project, not in the server solution):

```
cd development-source/tools/OfflineDaoc.Launcher
dotnet build OfflineDaoc.Launcher.csproj -c Release
```

- Output: `bin/Release/net10.0-windows/OfflineDAoC.dll` and `OfflineDAoC.exe`. Copy both into `runtime/` (with the
  launcher closed).
- The csproj compiles three server files into the launcher by relative path (`BotGoalSettings.cs`,
  `RealmEventRecordStore.cs`, `BotAiDelayReport.cs`). The path is `../../server/GameServer/...`,
  which works as cloned because the repo mirrors the dev layout.
- The version shown in the window title is `MainForm.DisplayVersion` ("0.1").
- The ADD BOTS work (`BotRoster.cs`), `TrueMode.cs` and `BotCharacterGenerator.cs` have no UI code. The cross-platform
  terminal launcher `development-source/tools/OfflineDaoc.Cli` (`net10.0`) compiles them too. Keep them free of WinForms.
  macOS: see [MACOS.md](MACOS.md).

---

## 2. Running the server

Normally through the launcher (`runtime/OfflineDAoC.exe`):

1. **START SERVER** checks the database flag (see below), runs the content refresh (section 3), then starts
   `runtime/server/CoreServer.exe --start -config=./config/serverconfig-165.xml` with `runtime/server` as the
   working folder. Standard input and output are redirected. The console goes to `runtime/logs/server-console.log`.
2. The server is "ready" when something listens on TCP port 10300. Then **ENTER REALM** is enabled.
3. **STOP SERVER** writes `exit` to the server's standard input and waits up to 10 seconds.

The launcher only runs True 1.65 Mode. There is no normal-mode switch:

- `TrueMode.Read` always rewrites `runtime/data/world-mode.txt` to:
  ```
  mode=1.65
  database=opendaoc-165.sqlite3.db
  config=config\serverconfig-165.xml
  ```
  The server and the new client read this file.
- `TrueMode.DatabaseAgrees` refuses to start unless `runtime/data/opendaoc-165.sqlite3.db` exists and its
  `ServerProperty` row `classic165_mode` is `True`.

`runtime/server/config/serverconfig-165.xml` sets:
- the server on 127.0.0.1:10300, region/UDP on 10400;
- SQLite: `Data Source=../data/opendaoc-165.sqlite3.db` (WAL, foreign keys on);
- script compilation to `./lib/GameServerScripts.dll`;
- `AutoAccountCreation`. A new account is only made on first login when both this setting and the server property
  `allow_auto_account_creation` are True (`LoginRequestHandler`). Releases need both True.

`runtime/server/config/serverconfig.xml` is a copy of the 1.65 config (it points at the same database).

At start the server logs `WORLD_MODE mode=1.65 state=...`. Per-mode state files live in `runtime/server/state-165/`:
`bot-goals.json`, `bot-world.json`, `rvr-world.json`, `rvr-relic-campaigns.json`, `realm-event-records.sqlite3`.
Short-lived launcher-to-server request files (`bot-world.request` and the like) stay in `runtime/server/`.

Running the server by hand (for debugging):

```
cd runtime/server
CoreServer.exe --start -config=./config/serverconfig-165.xml
```

Type `exit` to stop it. Run `OfflineDAoC.exe --refresh-165` first if the content files changed.

Other headless launcher options (both refuse while `CoreServer` runs):

- `OfflineDAoC.exe --refresh-165`: the content refresh only, then exit (code 0 = ok, 1 = failed). It writes
  `runtime/data/true165-refresh.log`.
- `OfflineDAoC.exe --add-bots 1.65 <realm 1-3> <count> <level>`: adds gamebot rows, the same rows the launcher's ADD
  BOTS buttons write. It writes `runtime/data/add-bots.log`. `normal` is refused.

---

## 3. How the True 1.65 content reaches the world database

All of this is in `development-source/tools/OfflineDaoc.Launcher/TrueMode.cs`.

### 3.1 The input files (`runtime/true165/`)

| File | Used by | What it is |
|---|---|---|
| `true165-rules.json` | launcher | `settings` (ServerProperty values), `remove` (what to archive), `starter_guilds`, `updates` (SQL). |
| `drops165.json` | launcher | Period monster drops: `monsters` → `{items: [[ItemTemplate id, chance %], ...], drop_count}`. |
| `add-*165.json` | launcher | Rows to add: `{table, key, reason, rows: [...]}`. |
| `progress-policy.json` | first build only | `ProgressTables` (saved progress) and `ClearTables` (logs and run state) to empty. |
| `release-world-defaults.json` | first build only | Every keep's realm and level and every door's health and state. |
| `coin165.json` | the **server** | Per-monster coin chance, read at run time by `mode165/Coin165.cs`. It is not written to the database. |

`true165-rules.json` in numbers (v0.1):
- 22 settings, for example `classic165_mode=True`, XP 1x, no free levels, no train anywhere, no guild buffs and
  `disabled_regions=27;165;233-242;244`;
- removal rules: 26 NPC classes, 2 packages, 13 names, 1 guild, the ROG loot generator and 16 SQL `where` rules;
- about 8,300 `updates` (period item stats, merchant stock repoints, levels, inn clearing, Shrouded Isles entrances
  and so on).

### 3.2 START SERVER and `--refresh-165`: `TrueMode.Refresh`

Both run the same code. It keeps all progress (characters, companions, bots, exchange, guilds, houses, keeps,
relics) and re-applies only the world content.

1. **Content stamp.** A SHA-256 over every `*.json` in `runtime/true165` (lower-case file name, a newline, then the
   bytes, files in case-insensitive name order). If it equals the `stamp` row in the table
   `offline_true165_content`, nothing happens. **Any** change to any JSON there triggers a refresh, even
   `coin165.json`.
2. **Backup.** The database is copied with the SQLite backup API to
   `runtime/data/mode-switch-backups/true165-<yyyyMMdd-HHmmss>.db`. The newest 3 are kept.
3. **One transaction:**
   1. `settings` → `ServerProperty` (update the row, or insert it);
   2. `remove` rules → each matching row is copied as JSON into `offline_true165_removed` (table, key, reason, row,
      time), then deleted. Nothing is lost: a row can be restored from its JSON;
   3. `updates` → each `sql` is run as is;
   4. the period drops are **replaced**: the `LootTemplate` rows tracked in `offline_true165_added` with reason
      `period drops:%` are deleted. Then `drops165.json` is applied again: each item joins the monster's own loot
      template (skipped if the template already has it), and `MobXLootTemplate.DropCount` is raised to
      `drop_count` (or the link row is created);
   5. `AddRows` for every `add-*.json`, in ordinal name order:
      - a row whose key is missing is inserted and tracked in `offline_true165_added` (SourceTable, RowKey, Reason,
        AddedAt);
      - a row whose key exists **and** is tracked for that table is updated to the file's values;
      - a row whose key exists but is not tracked (the world's own row) is left alone;
      - a tracked row of that table and reason that the file no longer lists is deleted (stale);
      - JSON properties with no matching column are skipped. `true`/`false` become 1/0;
   6. the new stamp and an `applied` time are written to `offline_true165_content`.
4. If anything throws, the transaction rolls back. START SERVER then shows an error and does not start the server.

Gotchas when you edit content:
- **Updates and removals run on every refresh.** Write `updates` SQL so that running it twice gives the same result
  (`UPDATE ... SET x = value WHERE ...`, never `x = x + 1`).
- **Don't change an add file's `reason` text** unless you mean to. Tracked rows are cleaned up by table and reason,
  so rows added under the old reason stop being cleaned up by that file.
- Keys must be unique and stable. The generators use prefixes such as `p165_item_<page key>`,
  `p165stock-...` and `p165floor-<path>-<n>`.
- Keep the two copies (`tools/claude-version/true165` and `runtime/true165`) identical. The generators write both. Only the
  runtime copy is hashed and applied.
- Dry run before you install: `tools/claude-version/true165/audit_fix_2026-10-31/coord/dryrun.py <content folder> <source db>
  <out db>` runs the same steps in Python on a copy. A second refresh should change nothing, and `PRAGMA
  quick_check` should say `ok`.

### 3.3 First build: `TrueMode.Derive` (not used in v0.1)

`Derive` built `opendaoc-165.sqlite3.db` from the normal-mode database. It copied the database, then ran:
- `FreshSlate`: empty the `progress-policy.json` tables except Guild/GuildRank/Account; zero guild progress; keep only
  the starter guilds; unown every house; reset keeps and doors from `release-world-defaults.json`; send relics home;
  set the population target to 0; turn MakeMeGM off;
- settings, removals, updates, drops and `AddRows`;
- VACUUM and `quick_check`;
- writing `serverconfig-165.xml`.

The Python twin is `tools/claude-version/true165/derive_true165_database.py` (with `content165.py`). Normal mode and its database
are archived, so nothing calls `Derive` now. v0.1 ships a ready clean world instead:
- `database/make_clean_db.py <world db>` makes the zip. It applies the same fresh-slate rules, keeps the content
  tracking tables (so the first START SERVER does not re-apply everything), turns auto accounts on and checks that
  every save table is empty;
- `database/unpack_db.py` unpacks it to `runtime/data/` (`--force` to overwrite).

---

## 4. The generators (`tools/claude-version/true165`)

They are development tools. They read research data or a **read-only copy** of a world database, and write JSON
into both content folders. Run them with `python -I`. None of them write to a live database.

| Script | Produces |
|---|---|
| `build_inns165.py` | `add-inns165.json` (60 inn NPCs: innkeepers, Horn Handlers, guards, traders, Realm Exchange brokers moved to the inns), `add-paths165.json`, `add-pathpoints165.json` (inn patrols). From the inn layouts. |
| `build_objects165.py` | `add-objects165.json` (Horn of Rallying, banners, props; the horn's ClassType is `DOL.GS.HornOfRallyingObject165`). |
| `build_innclear165.py` | "inn clearing" `updates` in `true165-rules.json`: wild monsters near an inn site, and aggressive ones near the route to it, moved aside and set on the navmesh floor. |
| `build_routefloor165.py <world db> [--write]` | `add-routefloor165.json` (horse routes re-stepped to follow bridge decks and dips) plus one `where` rule archiving the original points of those routes. |
| `build_coin165.py` | `coin165.json` (per-monster coin chance from Uthgard's measured kills, body-type average otherwise). |
| `build_drops165.py` | First-pass `drops165.json`. **Don't re-run it**: it would undo drops pass 2 (see `audit_fix_2026-10-31/README.txt`). |
| `build_classic_figures.py` | Writes the C# file `GameServer/mode165/ClassicFigures165.cs` (race/sex/face/hair → classic figure model) from the client's `fig3tofigmain.csv`. |
| `groundz165.py` | Helper: ground height from the navmeshes (`navmesh-165` first). |
| `content165.py`, `derive_true165_database.py` | Python version of the first build (section 3.3). |

`audit_fix_2026-10-31/` (spawn, drop and stat audit fixes; read its `README.txt`):
- `stats/`: about 7,200 item stat updates (period item pages);
- `spawns/`: `add-restoredspawns165.json` (period spawns restored), `add-npctemplates165.json`,
  `add-spectralprovisioner165.json`, level/faction updates;
- `drops/`: drops pass 2 (`drops165.json`), `add-dropitems165.json`, the freeshard loot `where` rules;
- `coinonly/`, `uthgard/`: `add-coinonlyitems165.json`, `add-coinonlyotd165.json`, `add-uthgarditems165.json`;
- `newspawns/`: `add-periodspawns165.json`;
- `fix_offmesh_spawns165.py`: moves restored or period spawns that stand off the main navmesh;
- `coord/dryrun.py`: the refresh dry run.

Several of these sub-scripts were written for a scratch folder, and their paths point there. Read before re-running.

`vendor_work/` (merchant stock; read its `README.txt` for the re-run order): `add-vendoritems165`,
`add-deaditems165`, `add-goodsitems165` (items), `add-merchantstock165`, `add-merchantlists165`,
`add-missingmerchantstock165`, `add-merchantgoods165` (stock), `add-missingmerchants165` (7 spawns), plus the
"period merchant ..." `updates`. These scripts need `world165_snapshot.db`: a read-only copy of a 1.65 world taken
**before** this content.

`add-epics165.json` (the Lord of Deceit target) and `add-trophies165.json` (8 boss trophy loot rows) have no
generator. They were written by hand.

Reference outputs that only the tools copy has (not applied): `drops165-missing.json`, `recipes165-report.md`,
`recipes165-unmatched.json`, `epics165-updates.json`, `rewards165-updates.json`.

---

## 5. Project rules (follow them; an LLM must too)

1. **Period sources first.** When period sources describe how a 1.65 system worked, follow them, even where the
   inherited server code differs:
   - period Allakhazam pages (Wayback, before 1 November 2003);
   - Camelot Warcry guides;
   - patch notes;
   - CapnBry radar data;
   - the real 1.64 client's data files.

   Use the inherited code and numbers only where the period sources are silent. Improvise only when both are unclear,
   and say so in the commit. For spell visuals and sounds, the 1.64 client's row is the truth: check it before
   "fixing" a missing glow or sound.
2. **No Trials of Atlantis or post-1.65 content or stats, anywhere.** That means no ToA bonus types (power pool %,
   stat or resist caps, Acuity, all-skills, unified Archery...), no later-patch stat values and no post-1.65
   regions, classes, races or systems. Item stats come from the period item page.
3. **Never change difficulty or combat numbers.** Monster levels, HP, damage, XP and combat formulas stay as they are,
   with no "keep it playable" floors or buffs. If something looks too hard or too easy, report it. The boss mechanic,
   damage and leash changes that let bots finish content, and Darkness Falls and Summoner's Hall, stay as they are.
4. **Bots follow the player rules.** Every rule a player lives by applies to gamebots and hired companions too:
   - crit, skill and resists;
   - crowd control;
   - wear and repairs;
   - ammo and poison;
   - death penalties;
   - resting rates;
   - looting.

   A `GameBot` is a `GameNPC`, so code that asks `is GamePlayer` gives bots the monster rules. In True 1.65 such checks
   use `PlayerRules165.IsPlayerLike`. No bot-only shortcuts: no free arrows, no instant loot, no fast rest.
5. **Quest QoL is kept as in normal mode.** Quest markers over NPCs, the yellow/red objective highlights and the quest
   guide (`/questguide`, `QuestGuide.cs`) work as before. Never gate them with `Classic165Mode.IsOn`, and never add
   quest-QoL settings to `true165-rules.json`. Removing quests or NPCs that are wrong for the period is fine.
6. **Drop policy: only high-likelihood period drops.**
   - Keep a drop when 2 or more sources agree, or a period page plainly shows it.
   - Remove every freeshard (Atlas/Phoenix-era) item and drop: AtlasXPItem, atlas_orbs_item, drmed, Atlas trophies,
     timed `Chance < 0` drops, items no source knows. No exceptions, even if it leaves a gap.
   - Uthgard's items, drops and measured rates **are** a valid source.
   - Fill gaps from period data: Allakhazam Known Loot and "Found in", CapnBry, then Uthgard rates.
   - Invent a rate or stat only as a last resort, keep it minimal, and mark it.
7. **Releases ship no accounts.**
   - The Account table is empty.
   - No characters or bots of the developer's.
   - No `account.txt` and no test-character file (`data/true165-test-characters.json`, which `TestKit165` uses to
     re-create test characters at every start).
   - Auto account creation is on (both settings, section 2).
8. **No builds while the server runs.**
   - `dotnet build`/`dotnet test` stall the live game loop (1-3 s NPC freezes).
   - Check for a running `CoreServer` in its own command and read the result before building. Never chain a build
     after the check.
   - Deploy, refresh and database writes also wait for a stopped server.
9. **CRLF line endings** and 4-space indents in C#, block-scoped namespaces (see `.editorconfig`). Keep a file's
   existing line endings when you edit it.
10. Keep True 1.65 rules behind `Classic165Mode.IsOn` where the code is shared with the inherited server. Content
    changes go in the `runtime/true165` data, not in hand edits to a world database.

---

## 6. Key code map (`development-source/server/GameServer`)

**`mode165/`**: the True 1.65 rules and systems.

| File | What |
|---|---|
| `Classic165Mode.cs` | `IsOn` (server property `classic165_mode`), XP locked to 1x, `state-165` paths, the freeshard start-up scripts that are off, GM-only commands (`/tele /tc /grind /freelevel /level /vault /autoloot /mobs /bounty* /classrog /buff /hardcore /xp /rp /dropmr /api /raid /spawn`), base-class career check, inn regions (1, 100, 200) as Realm Exchange regions, travel-time × 1.5. |
| `PlayerRules165.cs` | `IsPlayerLike`: bots get the player combat rules. |
| `DeathPenalty165.cs` | Bots lose Constitution on PvE release and get resurrection sickness. Gamebots pay a healer or wait it out. |
| `Supplies165.cs` | Real arrows/bolts from the bag, restock on town errands; companions get theirs from their player. |
| `ItemWear165.cs` | Bot weapons and armour lose condition like a player's; smith repairs. |
| `SoloRangedPull165.cs` | Solo bots pull aggressive monsters from range (spell, bow, thrown weapon) and wait for them. |
| `TownErrands165.cs` | Town trips for repairs, ammo and crafting (one per 30 min). |
| `BotLootWalk165.cs` | No autoloot: bots walk to the bags they may take; companions never loot. |
| `BaseClassBots165.cs` | Gamebots start as a base class and are promoted at level 5 (`PlannedClassId`). |
| `CrafterBots165.cs` | About 1 in 5 gamebots craft real recipes and list the products on the Realm Exchange; siegecrafters build siege kits. |
| `Charm165.cs` | Bots charm real monsters with learned charm spells only. |
| `Coin165.cs` | Per-monster coin chance from `runtime/true165/coin165.json`. |
| `HiredCompanions165.cs`, `Hire165.cs`, `CompanionWindows165.cs`, `CompanionGearVault165.cs`, `CompanionUiCommand.cs`, `CompanionChat165.cs` | Hired companions: hiring window (DebugMode 0x21 'H' packets), gear/spec/stats windows, `/companionui`, persistence (`offline_hired_companions`). |
| `Innkeeper165.cs`, `HornHandler165.cs`, `Adventurer165.cs` (`Adventurer165`, `Traveler165`), `Courier165.cs`, `InnQuestMarkers165.cs` | The traveler's inns: innkeeper (hiring), Horn of Rallying (player-started PvE events), road patrols and inn travelers, the level-5/50 courier quests and their map markers. |
| `Exchange165.cs` | Realm Exchange pricing and relisting (90%, 80%, then returned). |
| `Epics165.cs` | Period level-50 epic finales (Lord of Deceit, Symbol of the Broken, Last Heir). |
| `ClassicFigures165.cs` | Generated; classic face/hair figure models. |
| `TestKit165.cs` | Local test characters (only when the test file exists; never shipped). |

**`bots/`**: the gamebot itself.
- `GameBot.cs`, `BotBrain*.cs` (combat, crowd control, pulls);
- `BotEquipment.cs`, `BotInventory.cs`;
- `BotRestRecovery.cs`;
- `Companion*.cs` (companion follow, healer hold, PvP, stealth);
- `BotManager.cs`, `BotCommand.cs`.

**`bots/autonomous/`** (about 160 files): the world population AI.
- `AutonomousWorldBotController*.cs`: goals, camps, shopping, town trips, travel, RvR, relics, sieges, expeditions;
- `AutonomousBotGroupCoordinator*.cs`: grouping;
- `AutonomousRvr*.cs`, `AutonomousRealmRaid.cs`, `RealmRaid*.cs`: RvR events and raids;
- `AutonomousZone*.cs`: routing over roads, seams and pockets in `runtime/server/navmesh/*.json`;
- `AutonomousPopulationController.cs`: the population ramp;
- `BotGoalSettings.cs`: shared with the launcher.

Useful log tags:
- `WORLD_MODE`, `BOT_DEATH_165`, `BOT_PROMOTED_165`;
- `BOT_REPAIRED_165`, `BOT_HEALER_165`, `AUTONOMOUS_SERVICE_ARRIVED`, `AUTONOMOUS_SHOPPING_INDEX`;
- `COMPANIONUI_CMD`, `CLICK_165`;
- `RVR_RELIC_CAMPAIGN`, `RELIC_CAPTURE`.

---

## 7. The release (v0.1)

A release is the clean runtime folder plus the new client, as checked parts with a downloader. v0.1 was made this
way; the tools and the steps are in [release/README.md](../release/README.md), and its baseline is in
`release-baselines/0.1/`.

`development-source/tools/build_release_true165.py` builds the game package from a working install: `runtime/`
(the launcher, server, both navmesh sets, the True 1.65 content and the clean world), `tools/dotnet` (.NET 10:
the host and the NETCore, WindowsDesktop and AspNetCore runtimes) and `START OFFLINE DAOC TRUE 1.65.cmd`. It leaves
out everything in the never-ships list below. The new client is packaged separately into `new-client/` beside
`runtime/`.

**Ships:**
- the launcher, with SQLite;
- the server binaries (`runtime/server` and `lib/`);
- the configs, with `AutoAccountCreation` set to **True** in `serverconfig-165.xml`;
- the full `navmesh` and `navmesh-165`;
- the content files (`classic-quests.json`, `classic-quest-guides.json`, `classic-otd.json`);
- `runtime/true165`;
- the clean world from `database/make_clean_db.py`;
- the new client (`new-client/`, with no `save/` folder and no `dev_mode.txt`);
- a bundled .NET 10 in `tools/dotnet`;
- `START OFFLINE DAOC TRUE 1.65.cmd`, which sets `DOTNET_ROOT`, `DOTNET_ROOT_X64` and `DOTNET_MULTILEVEL_LOOKUP=0`,
  then starts `runtime/OfflineDAoC.exe`.

**Never ships:**
- logs, every `*.before-*` backup, `deployment-backups`, `mode-switch-backups`, `keep-relic-reset-backups`;
- `account.txt` and any test-account or test-character file (`true165-test-characters.json*`);
- the development world database and its `-wal`/`-shm` files;
- the contents of `state-165`;
- the original client (`client-opendaoc`) and DXVK;
- the stale self-contained `runtime/win-x64` launcher publish.

**Checks before packing:**
- the clean world has 0 rows in every save table and an empty Account table;
- keeps, doors and relics match `release-world-defaults.json`;
- `classic165_mode=True`, XP 1x, MakeMeGM off, population target 0;
- `quick_check` is ok;
- a fresh install makes its own login on first ENTER REALM, and the server creates the account.

**How the launcher finds the client** (`NewClient.cs`): `runtime/new-client.txt` (the first non-comment line), then
`../new-client` beside `runtime/`, then the development folder. A client folder counts when it has
`godot/project.godot` and a `tools/godot_net/Godot_*_mono_win64.exe`. ENTER REALM starts:

```
<engine> --path <client>/godot -- --server=127.0.0.1 --account=<account> --password=<password> --world-mode=1.65
```

It runs with `APPDATA` set to `<client>/save` (settings, key bindings and logs stay in the client folder), and with
the client's own `tools/dotnet` when it has one.
