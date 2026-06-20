using System.Text;
using Iptv.Core.Channels;

namespace Iptv.Persistence.CustomGroups;

public sealed record CustomGroupCsvRow(Guid ChannelId, string DisplayName, string? CustomGroup);

public sealed class CustomGroupCsvService
{
    private const long MaximumImportBytes = 16L * 1024 * 1024;

    public async Task ExportAsync(string path, IEnumerable<Channel> channels, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Export path is required.", nameof(path));
        }

        ArgumentNullException.ThrowIfNull(channels);

        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);
        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using var stream = File.Create(tempPath);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync("channelId,displayName,customGroup").ConfigureAwait(false);
            foreach (Channel channel in channels.OrderBy(channel => channel.ImportIndex).ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string line = string.Join(',',
                    Escape(channel.Id.ToString()),
                    Escape(channel.DisplayName),
                    Escape(channel.CustomGroup ?? string.Empty));
                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public async Task<IReadOnlyList<CustomGroupCsvRow>> ImportAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Import path is required.", nameof(path));
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Custom group CSV file was not found.", path);
        }

        if (fileInfo.Length > MaximumImportBytes)
        {
            throw new InvalidDataException("Custom group CSV is too large to import safely.");
        }

        var rows = new List<CustomGroupCsvRow>();
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? header = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (header is null || !header.Contains("channelId", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Custom group CSV header must include channelId, displayName, customGroup.");
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] fields = ParseLine(line).ToArray();
            if (fields.Length < 3 || !Guid.TryParse(fields[0], out Guid channelId) || channelId == Guid.Empty)
            {
                continue;
            }

            rows.Add(new CustomGroupCsvRow(
                channelId,
                fields[1],
                NormalizeGroup(fields[2])));
        }

        return rows
            .GroupBy(row => row.ChannelId)
            .Select(group => group.Last())
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static IEnumerable<string> ParseLine(string line)
    {
        var field = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char current = line[index];
            if (quoted)
            {
                if (current == '"' && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                    continue;
                }

                if (current == '"')
                {
                    quoted = false;
                    continue;
                }

                field.Append(current);
                continue;
            }

            if (current == '"')
            {
                quoted = true;
                continue;
            }

            if (current == ',')
            {
                yield return field.ToString();
                field.Clear();
                continue;
            }

            field.Append(current);
        }

        yield return field.ToString();
    }
}
