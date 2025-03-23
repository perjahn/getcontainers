using System;
using System.Collections.Generic;

class ConfigReader
{
    public static KubeConfig Parse(string[] rows)
    {
        Console.WriteLine(rows);

        var result = new Dictionary<string, object?>();

        var linenum = 1;
        foreach (var line in rows)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
            {
                continue;
            }

            var parts = trimmedLine.Split([':'], 2);
            if (parts.Length != 2)
            {
                throw new FormatException($"Invalid yaml file on line {linenum}: '{line}'");
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            result[key] = value switch
            {
                _ when int.TryParse(value, out int intValue) => intValue,
                _ when bool.TryParse(value, out bool boolValue) => boolValue,
                _ when value.Equals("null", StringComparison.OrdinalIgnoreCase) => null,
                _ => value
            };

            linenum++;
        }

        return new KubeConfig();
    }
}
