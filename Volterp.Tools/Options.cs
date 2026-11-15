namespace Volterp.Tools;

// The options of the command: --name value. A flag without a value is read as
// "true", so a boolean option does not need a special case.
internal static class Options
{
    public static Dictionary<string, string> Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                continue;

            var key = args[i][2..];

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[key] = args[++i];
                continue;
            }

            values[key] = "true";
        }

        return values;
    }
}
