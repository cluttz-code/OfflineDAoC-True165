using System.Data.SQLite;

namespace OfflineDaoc.Launcher;

/// <summary>
/// The world bot roster's database work: the ADD BOTS buttons' new bots and the population target. Shared by the
/// Windows launcher (MainForm) and the cross-platform command-line launcher (OfflineDaoc.Cli), so it has no UI code.
/// </summary>
internal static class BotRoster
{
    internal static bool HasColumn(SQLiteConnection connection, SQLiteTransaction transaction, string table, string column)
    {
        using var info = connection.CreateCommand();
        info.Transaction = transaction;
        info.CommandText = $"PRAGMA table_info(\"{table}\")";
        using SQLiteDataReader reader = info.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>The ADD BOTS buttons' work, also run headless by --add-bots (Program.cs) for test rosters.</summary>
    internal static IReadOnlyList<BotCharacterGenerator.Identity> GenerateBotCharacters(string database, bool is165, int realm, int count, int level = 1)
    {
        if (!File.Exists(database))
            throw new InvalidOperationException("The prepared world database is missing.");
        if (count is not (1 or 10 or 100))
            throw new ArgumentOutOfRangeException(nameof(count));
        if (level is not (1 or 50)) throw new ArgumentOutOfRangeException(nameof(level));

        using var connection = new SQLiteConnection($"Data Source={database};Version=3;Pooling=False;Default Timeout=10");
        connection.Open();
        using (var busy = connection.CreateCommand())
        {
            busy.CommandText = "PRAGMA busy_timeout=10000; PRAGMA foreign_keys=OFF;";
            busy.ExecuteNonQuery();
        }

        using var transaction = connection.BeginTransaction();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var names = connection.CreateCommand())
        {
            names.Transaction = transaction;
            names.CommandText = "SELECT Name FROM offline_world_bots UNION SELECT Name FROM DOLCharacters";
            using var reader = names.ExecuteReader();
            while (reader.Read()) reserved.Add(reader.GetString(0));
        }

        // True 1.65 Mode: a new level-1 bot starts as the base class of the class it rolled, which it keeps as its
        // planned class (column added by the server; until the server has run once, the server converts it instead).
        if (level == 1 && is165 && !HasColumn(connection, transaction, "offline_world_bots", "PlannedClassId"))
        {
            // the server adds this column at its first start in this build; bots made before then need it too
            using var add = connection.CreateCommand();
            add.Transaction = transaction;
            add.CommandText = "ALTER TABLE offline_world_bots ADD COLUMN PlannedClassId INT(11) NOT NULL DEFAULT 0";
            add.ExecuteNonQuery();
        }
        bool basePlan = level == 1 && is165 && HasColumn(connection, transaction, "offline_world_bots", "PlannedClassId");
        var identities = new List<BotCharacterGenerator.Identity>(count);
        for (int index = 0; index < count; index++)
        {
            BotCharacterGenerator.Identity identity = BotCharacterGenerator.Generate(realm, reserved, is165);
            int plannedClass = 0;
            if (basePlan && BotCharacterGenerator.BaseClass(identity.ClassId) is { } baseClass)
            {
                plannedClass = identity.ClassId;
                identity = identity with { ClassId = baseClass.Id, ClassName = baseClass.Name };
            }
            BotStartingLocation start = level == 50
                ? CapitalBotStartingLocation(identity.Realm)
                : ChooseBotStartingLocation(connection, transaction, identity.Realm, identity.RaceId, identity.ClassId, plannedClass);
            identities.Add(identity);
            string now = DateTime.UtcNow.ToString("O");
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO offline_world_bots
                    (Name, Realm, ClassId, ClassName, RaceId, RaceName, Gender, Level, Experience, RealmPoints,
                     ZoneName, Activity, IsOnline, IsAlive, LastUpdateUtc, MoneyCopper, InventoryRevision,
                     ZoneId, X, Y, Z, RegionId, Health, Mana, Endurance, BindRegionId, BindX, BindY, BindZ,
                     CurrentGoal, ObjectiveProgress, LastMeaningfulProgressUtc, IsRetired)
                VALUES
                    (@name, @realm, @classId, @className, @raceId, @raceName, @gender, @level, @xp, 0,
                     @zoneName, 'Queued at randomized starting location', 0, 1, @now, @money, 0,
                     @zoneId, @x, @y, @z, @region, 1, 0, 0, @region, @x, @y, @z,
                     'Awaiting staggered login queue', @placement, @now, 0)
                """;
            if (basePlan)
                insert.CommandText = insert.CommandText.Replace("IsRetired)", "IsRetired, PlannedClassId)").Replace("@now, 0)", "@now, 0, @planned)");
            insert.Parameters.AddWithValue("@planned", plannedClass);
            insert.Parameters.AddWithValue("@name", identity.Name);
            insert.Parameters.AddWithValue("@level", level);
            insert.Parameters.AddWithValue("@xp", level == 50 ? 169999999950L : 0L);
            insert.Parameters.AddWithValue("@money", level == 50 ? 100000000L : 0L);
            insert.Parameters.AddWithValue("@realm", identity.Realm);
            insert.Parameters.AddWithValue("@classId", identity.ClassId);
            insert.Parameters.AddWithValue("@className", identity.ClassName);
            insert.Parameters.AddWithValue("@raceId", identity.RaceId);
            insert.Parameters.AddWithValue("@raceName", identity.RaceName);
            insert.Parameters.AddWithValue("@gender", identity.Gender);
            insert.Parameters.AddWithValue("@now", now);
            insert.Parameters.AddWithValue("@region", start.RegionId);
            insert.Parameters.AddWithValue("@x", start.X);
            insert.Parameters.AddWithValue("@y", start.Y);
            insert.Parameters.AddWithValue("@z", start.Z);
            insert.Parameters.AddWithValue("@zoneId", start.ZoneId);
            insert.Parameters.AddWithValue("@zoneName", level == 50 ? start.ZoneName : start.RegionId is 51 or 151 or 181
                ? "Shrouded Isles starting area"
                : "Classic starting area");
            insert.Parameters.AddWithValue("@placement",
                $"Launcher assigned a realm/race/class-valid random start in region {start.RegionId} before server startup");
            insert.ExecuteNonQuery();
            if (level == 50)
            {
                long botId = connection.LastInsertRowId;
                using var gear = connection.CreateCommand();
                gear.Transaction = transaction;
                gear.CommandText = """
                    INSERT INTO Inventory (Inventory_ID,OwnerID,ITemplate_Id,SlotPosition,Count,Condition,Durability,LastTimeRowUpdated)
                    SELECT lower(hex(randomblob(16))),@owner,l.TemplateId,l.SlotPosition,1,t.MaxCondition,t.MaxDurability,@now
                    FROM offline_level50_loadouts l JOIN ItemTemplate t ON t.Id_nb=l.TemplateId WHERE l.ClassId=@class
                    """;
                gear.Parameters.AddWithValue("@owner", $"offlinebot:{botId}");
                gear.Parameters.AddWithValue("@now", now);
                gear.Parameters.AddWithValue("@class", identity.ClassId);
                if (gear.ExecuteNonQuery() < 15) throw new InvalidOperationException("The level-50 class template is missing or incomplete. No bots were added.");
            }
        }

        UpdatePopulationTarget(connection, transaction);
        transaction.Commit();
        return identities;
    }

    internal sealed record BotStartingLocation(int RegionId, int X, int Y, int Z, int ZoneId = 0, string ZoneName = "");

    // Match the server's established capital recovery anchors. Its normal
    // login validation grounds these positions on the current navmesh.
    internal static BotStartingLocation CapitalBotStartingLocation(int realm) => realm switch
    {
        1 => new(10, 35990, 30298, 8000, 26, "City of Camelot"),
        2 => new(101, 32020, 28294, 8819, 120, "Jordheim"),
        3 => new(201, 33197, 31200, 8000, 209, "Tir na Nog"),
        _ => throw new ArgumentOutOfRangeException(nameof(realm)),
    };

    internal static BotStartingLocation ChooseBotStartingLocation(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        int realm,
        int raceId,
        int classId,
        int plannedClassId = 0)
    {
        List<BotStartingLocation> starts = FindBotStartingLocations(connection, transaction, realm, raceId, classId);
        // True 1.65 Mode base classes: a race and base class pair the table has no row for (a Saracen Disciple) starts
        // where that race starts as the planned advanced class (Saracen Necromancer: Caer Gothwaite)
        if (starts.Count == 0 && plannedClassId > 0 && plannedClassId != classId)
            starts = FindBotStartingLocations(connection, transaction, realm, raceId, plannedClassId);

        if (starts.Count == 0)
        {
            throw new InvalidOperationException(
                $"No authoritative Classic/Shrouded Isles starting location exists for realm {realm}, race {raceId}, class {classId}" +
                (plannedClassId > 0 && plannedClassId != classId ? $" (planned class {plannedClassId})" : "") + ". No capital fallback was used.");
        }

        return starts[Random.Shared.Next(starts.Count)];
    }

    internal static List<BotStartingLocation> FindBotStartingLocations(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        int realm,
        int raceId,
        int classId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Region, XPos, YPos, ZPos
            FROM StartupLocation
            WHERE MinVersion <= 168
              AND (RealmID=0 OR RealmID=@realm)
              AND (RaceID=0 OR RaceID=@race)
              AND (ClassID=0 OR ClassID=@class)
              AND (XPos<>0 OR YPos<>0 OR ZPos<>0)
              AND ((@realm=1 AND Region IN (1,51))
                OR (@realm=2 AND Region IN (100,151))
                OR (@realm=3 AND Region IN (200,181)))
            GROUP BY Region, XPos, YPos, ZPos
            """;
        command.Parameters.AddWithValue("@realm", realm);
        command.Parameters.AddWithValue("@race", raceId);
        command.Parameters.AddWithValue("@class", classId);

        var starts = new List<BotStartingLocation>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            starts.Add(new BotStartingLocation(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));
        }
        return starts;
    }

    internal static void UpdatePopulationTarget(SQLiteConnection connection, SQLiteTransaction transaction)
    {
        int count;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.Transaction = transaction;
            countCommand.CommandText = "SELECT COUNT(*) FROM offline_world_bots WHERE IsRetired=0" + NotHiredCompanion(connection, transaction, "BotId");
            count = Convert.ToInt32(countCommand.ExecuteScalar());
        }
        int target = count;
        using (var settings = connection.CreateCommand())
        {
            settings.Transaction = transaction;
            settings.CommandText = "UPDATE offline_population_settings SET Value=CASE Key WHEN 'PopulationEnabled' THEN @enabled WHEN 'ActiveTarget' THEN @target WHEN 'HardActiveCap' THEN '0' ELSE Value END WHERE Key IN ('PopulationEnabled','ActiveTarget','HardActiveCap')";
            settings.Parameters.AddWithValue("@enabled", count > 0 ? "true" : "false");
            settings.Parameters.AddWithValue("@target", target.ToString());
            settings.ExecuteNonQuery();
        }
        using (var serverProperties = connection.CreateCommand())
        {
            serverProperties.Transaction = transaction;
            serverProperties.CommandText = "UPDATE ServerProperty SET Value=CASE Key WHEN 'population_enabled' THEN @enabled WHEN 'active_target' THEN @target WHEN 'hard_active_cap' THEN '0' ELSE Value END WHERE Key IN ('population_enabled','active_target','hard_active_cap')";
            serverProperties.Parameters.AddWithValue("@enabled", count > 0 ? "true" : "false");
            serverProperties.Parameters.AddWithValue("@target", target.ToString());
            serverProperties.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// True 1.65 Mode's hired companions are stored as bot records, but they belong to their players, not the world's bot
    /// roster: the launcher neither lists, counts nor deletes them (owner 2026-10-30: "1001 roster on Albion").
    /// Empty when the table doesn't exist (normal mode).
    /// </summary>
    internal static string NotHiredCompanion(SQLiteConnection connection, SQLiteTransaction? transaction, string botIdColumn)
    {
        using var command = connection.CreateCommand();
        if (transaction != null)
            command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='offline_hired_companions'";
        return command.ExecuteScalar() == null ? string.Empty
            : $" AND {botIdColumn} NOT IN (SELECT BotId FROM offline_hired_companions)";
    }
}
