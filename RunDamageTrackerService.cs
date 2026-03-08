using System.Globalization;
using System.Text;

namespace DamageTracker;

public static class RunDamageTrackerService
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<ulong, PlayerDamageSnapshot> Totals = new();

    private static string? _currentRunToken;
    private static int _combatIndex;
    private static bool _combatActive;

    public static event Action<string>? Changed;

    public static void BeginRun(object? runState)
    {
        string nextToken = ReflectionHelpers.ResolveRunToken(runState);

        lock (SyncRoot)
        {
            if (string.Equals(_currentRunToken, nextToken, StringComparison.Ordinal))
            {
                return;
            }

            Totals.Clear();
            _currentRunToken = nextToken;
            _combatIndex = 0;
            _combatActive = false;
        }

        Publish();
    }

    public static void BeginCombat(object? combatState)
    {
        lock (SyncRoot)
        {
            _combatIndex++;
            _combatActive = combatState != null;

            foreach (PlayerDamageSnapshot snapshot in Totals.Values)
            {
                snapshot.CombatDamage = 0m;
            }
        }

        Publish();
    }

    public static void EndCombat()
    {
        lock (SyncRoot)
        {
            _combatActive = false;
        }

        Publish();
    }

    public static void NotePlayer(object? player)
    {
        if (!ReflectionHelpers.TryResolvePlayerHandle(player, out PlayerHandle handle))
        {
            return;
        }

        lock (SyncRoot)
        {
            PlayerDamageSnapshot snapshot = GetOrCreate(handle);
            if (!string.IsNullOrWhiteSpace(handle.DisplayName))
            {
                snapshot.DisplayName = handle.DisplayName;
            }
        }

        Publish();
    }

    public static void RecordDamage(object? dealer, object? result, object? target, object? cardSource)
    {
        if (!ReflectionHelpers.TryResolveDamageAmount(result, out decimal damage) || damage <= 0)
        {
            return;
        }

        if (!ReflectionHelpers.TryResolvePlayerHandle(dealer, out PlayerHandle handle))
        {
            if (!ReflectionHelpers.TryResolvePlayerHandle(cardSource, out handle))
            {
                if (!ReflectionHelpers.TryResolvePlayerHandle(target, out handle))
                {
                    return;
                }
            }
        }

        lock (SyncRoot)
        {
            PlayerDamageSnapshot snapshot = GetOrCreate(handle);
            snapshot.TotalDamage += damage;
            snapshot.CombatDamage += damage;
            snapshot.LastDamage = damage;
            snapshot.LastUpdatedUtc = DateTime.UtcNow;
        }

        Publish();
    }

    public static string BuildOverlayText()
    {
        List<PlayerDamageSnapshot> snapshots;
        string runToken;
        int combatIndex;
        bool combatActive;

        lock (SyncRoot)
        {
            snapshots = Totals.Values
                .OrderByDescending(snapshot => snapshot.TotalDamage)
                .ThenBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(snapshot => snapshot.Clone())
                .ToList();
            runToken = _currentRunToken ?? "unknown-run";
            combatIndex = _combatIndex;
            combatActive = _combatActive;
        }

        StringBuilder builder = new();
        builder.Append("[color=#93A9C3]RUN[/color]  [b]")
            .Append(EscapeBbCode(runToken))
            .AppendLine("[/b]");
        builder.Append("[color=#93A9C3]COMBAT[/color]  [b]")
            .Append(combatIndex)
            .Append(combatActive ? "[/b]  [color=#7BE0A8](live)[/color]" : "[/b]  [color=#C7B0FF](idle)[/color]")
            .AppendLine();
        builder.AppendLine();

        if (snapshots.Count == 0)
        {
            builder.AppendLine("[color=#EAF2FF]No player damage recorded yet.[/color]");
            builder.AppendLine("[color=#93A9C3]Damage starts updating after the first hit lands.[/color]");
            return builder.ToString();
        }

        for (int index = 0; index < snapshots.Count; index++)
        {
            PlayerDamageSnapshot snapshot = snapshots[index];
            builder.Append("[b]")
                .Append(index + 1)
                .Append(". ")
                .Append(EscapeBbCode(snapshot.DisplayName))
                .AppendLine("[/b]");
            builder.Append("[color=#93A9C3]Total[/color] [b]")
                .Append(Format(snapshot.TotalDamage))
                .Append("[/b]    [color=#93A9C3]Combat[/color] [b]")
                .Append(Format(snapshot.CombatDamage))
                .Append("[/b]    [color=#93A9C3]Last[/color] [b]")
                .Append(Format(snapshot.LastDamage))
                .AppendLine("[/b]");

            if (index < snapshots.Count - 1)
            {
                builder.AppendLine("[color=#243548]--------------------------------[/color]");
            }
        }

        return builder.ToString();
    }

    private static PlayerDamageSnapshot GetOrCreate(PlayerHandle handle)
    {
        if (Totals.TryGetValue(handle.PlayerKey, out PlayerDamageSnapshot? existing))
        {
            return existing;
        }

        PlayerDamageSnapshot created = new()
        {
            PlayerKey = handle.PlayerKey,
            DisplayName = handle.DisplayName
        };
        Totals.Add(handle.PlayerKey, created);
        return created;
    }

    private static string Format(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string EscapeBbCode(string value)
    {
        return value.Replace("[", "[[", StringComparison.Ordinal).Replace("]", "]]", StringComparison.Ordinal);
    }

    private static void Publish()
    {
        Changed?.Invoke(BuildOverlayText());
    }
}

public sealed class PlayerDamageSnapshot
{
    public ulong PlayerKey { get; set; }

    public string DisplayName { get; set; } = "Unknown Player";

    public decimal TotalDamage { get; set; }

    public decimal CombatDamage { get; set; }

    public decimal LastDamage { get; set; }

    public DateTime LastUpdatedUtc { get; set; }

    public PlayerDamageSnapshot Clone()
    {
        return new PlayerDamageSnapshot
        {
            PlayerKey = PlayerKey,
            DisplayName = DisplayName,
            TotalDamage = TotalDamage,
            CombatDamage = CombatDamage,
            LastDamage = LastDamage,
            LastUpdatedUtc = LastUpdatedUtc
        };
    }
}

public readonly record struct PlayerHandle(ulong PlayerKey, string DisplayName);