using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Platform;

namespace DamageTracker;

internal static class ReflectionHelpers
{
    private static readonly string[] PlayerLinks =
    {
        "Player",
        "OwnerPlayer",
        "Owner",
        "Controller",
        "Summoner",
        "SourcePlayer",
        "Creature",
        "Dealer"
    };

    private static readonly string[] IdMembers =
    {
        "NetId",
        "PlayerId",
        "LocalPlayerId",
        "Id"
    };

    private static readonly string[] NameMembers =
    {
        "DisplayName",
        "CharacterName",
        "Name",
        "LocalizedName"
    };

    private static readonly string[] DamageMembers =
    {
        "DamageDealt",
        "FinalDamage",
        "ActualDamage",
        "UnblockedDamage",
        "Amount",
        "Damage"
    };

    public static string ResolveRunToken(object? runState)
    {
        if (runState == null)
        {
            return "unknown-run";
        }

        foreach (string memberName in new[] { "Seed", "RunId", "Id" })
        {
            object? value = TryGetMemberValue(runState, memberName);
            if (value != null)
            {
                string text = value.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return $"run-{RuntimeHelpers.GetHashCode(runState)}";
    }

    public static bool TryResolvePlayerHandle(object? source, out PlayerHandle handle)
    {
        if (source == null)
        {
            handle = default;
            return false;
        }

        object? playerCandidate = FindPlayerCandidate(source, 0);
        if (playerCandidate == null)
        {
            handle = default;
            return false;
        }

        ulong playerKey = TryGetUnsignedId(playerCandidate) ?? (ulong)RuntimeHelpers.GetHashCode(playerCandidate);
        string displayName = TryGetPlatformDisplayName(playerKey)
            ?? TryGetDisplayName(playerCandidate)
            ?? $"Player {playerKey}";

        handle = new PlayerHandle(playerKey, displayName);
        return true;
    }

    public static bool TryResolveDamageAmount(object? value, out decimal amount)
    {
        if (TryConvertToDecimal(value, out amount))
        {
            return true;
        }

        if (value != null)
        {
            foreach (string memberName in DamageMembers)
            {
                if (TryConvertToDecimal(TryGetMemberValue(value, memberName), out amount))
                {
                    return true;
                }
            }
        }

        amount = 0m;
        return false;
    }

    private static object? FindPlayerCandidate(object source, int depth)
    {
        if (depth > 3)
        {
            return null;
        }

        Type type = source.GetType();
        if (type.Name.Contains("Player", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        foreach (string link in PlayerLinks)
        {
            object? next = TryGetMemberValue(source, link);
            if (next == null || ReferenceEquals(next, source))
            {
                continue;
            }

            object? resolved = FindPlayerCandidate(next, depth + 1);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static ulong? TryGetUnsignedId(object source)
    {
        foreach (string memberName in IdMembers)
        {
            object? value = TryGetMemberValue(source, memberName);
            if (value == null)
            {
                continue;
            }

            if (value is ulong unsignedLong)
            {
                return unsignedLong;
            }

            if (value is uint unsignedInt)
            {
                return unsignedInt;
            }

            if (value is long signedLong && signedLong >= 0)
            {
                return (ulong)signedLong;
            }

            if (value is int signedInt && signedInt >= 0)
            {
                return (ulong)signedInt;
            }

            if (ulong.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? TryGetDisplayName(object source)
    {
        foreach (string memberName in NameMembers)
        {
            object? value = TryGetMemberValue(source, memberName);
            string? text = value?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static string? TryGetPlatformDisplayName(ulong playerKey)
    {
        try
        {
            PlatformType platformType = PlatformUtil.PrimaryPlatform;
            if (platformType == PlatformType.None)
            {
                return null;
            }

            string? personaName = PlatformUtil.GetPlayerName(platformType, playerKey);

            if (string.IsNullOrWhiteSpace(personaName)
                || string.Equals(personaName, "[unknown]", StringComparison.OrdinalIgnoreCase)
                || string.Equals(personaName, playerKey.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return null;
            }

            return personaName;
        }
        catch
        {
            return null;
        }
    }

    private static object? TryGetMemberValue(object source, string memberName)
    {
        Type type = source.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo? property = type.GetProperty(memberName, flags);
        if (property != null)
        {
            try
            {
                return property.GetValue(source);
            }
            catch
            {
                return null;
            }
        }

        FieldInfo? field = type.GetField(memberName, flags);
        if (field != null)
        {
            try
            {
                return field.GetValue(source);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static bool TryConvertToDecimal(object? value, out decimal amount)
    {
        switch (value)
        {
            case decimal decimalValue:
                amount = decimalValue;
                return true;
            case double doubleValue:
                amount = (decimal)doubleValue;
                return true;
            case float floatValue:
                amount = (decimal)floatValue;
                return true;
            case long longValue:
                amount = longValue;
                return true;
            case int intValue:
                amount = intValue;
                return true;
            case short shortValue:
                amount = shortValue;
                return true;
            case byte byteValue:
                amount = byteValue;
                return true;
            default:
                if (value != null && decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
                {
                    amount = parsed;
                    return true;
                }

                amount = 0m;
                return false;
        }
    }
}