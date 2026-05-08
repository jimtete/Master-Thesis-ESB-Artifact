using System.Globalization;
using System.Text.RegularExpressions;

namespace OlympusServiceBus.Engine.Scheduling;

internal static partial class TimeZoneResolver
{
    private const int MaximumUtcOffsetHours = 14;

    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        var trimmedTimeZoneId = timeZoneId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(trimmedTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return ResolveCustomUtcOffset(trimmedTimeZoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return ResolveCustomUtcOffset(trimmedTimeZoneId);
        }
    }

    private static TimeZoneInfo ResolveCustomUtcOffset(string timeZoneId)
    {
        var match = UtcOffsetPattern().Match(timeZoneId);
        if (!match.Success)
        {
            throw new TimeZoneNotFoundException($"The time zone ID '{timeZoneId}' was not found on the local computer.");
        }

        var sign = match.Groups["sign"].Value == "-" ? -1 : 1;
        var hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        var minutes = match.Groups["minutes"].Success
            ? int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture)
            : 0;

        if (hours > MaximumUtcOffsetHours || minutes >= 60)
        {
            throw new TimeZoneNotFoundException($"The time zone ID '{timeZoneId}' was not found on the local computer.");
        }

        var offset = new TimeSpan(sign * hours, sign * minutes, 0);
        var normalizedId = $"{match.Groups["prefix"].Value.ToUpperInvariant()}{(sign < 0 ? "-" : "+")}{hours:00}:{minutes:00}";

        return TimeZoneInfo.CreateCustomTimeZone(
            normalizedId,
            offset,
            normalizedId,
            normalizedId);
    }

    [GeneratedRegex(@"^(?<prefix>UTC|GMT)\s*(?<sign>[+-])\s*(?<hours>\d{1,2})(?::?(?<minutes>\d{2}))?$", RegexOptions.IgnoreCase)]
    private static partial Regex UtcOffsetPattern();
}
