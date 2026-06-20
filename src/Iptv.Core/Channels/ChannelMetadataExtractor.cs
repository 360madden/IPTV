namespace Iptv.Core.Channels;

public static class ChannelMetadataExtractor
{
    public static int? TryInferReleaseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        ReadOnlySpan<char> span = value.AsSpan();
        for (int index = 0; index <= span.Length - 4; index++)
        {
            if (!char.IsDigit(span[index]) ||
                !char.IsDigit(span[index + 1]) ||
                !char.IsDigit(span[index + 2]) ||
                !char.IsDigit(span[index + 3]))
            {
                continue;
            }

            int year =
                ((span[index] - '0') * 1000) +
                ((span[index + 1] - '0') * 100) +
                ((span[index + 2] - '0') * 10) +
                (span[index + 3] - '0');
            if (year is >= 1900 and <= 2100)
            {
                return year;
            }
        }

        return null;
    }
}
