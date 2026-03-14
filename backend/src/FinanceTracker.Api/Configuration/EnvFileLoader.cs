namespace FinanceTracker.Api.Configuration;

public static class EnvFileLoader
{
    public static void LoadIfPresent(string basePath, string fileName = ".env")
    {
        var path = Path.Combine(basePath, fileName);
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
