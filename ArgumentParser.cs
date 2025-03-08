using System.Collections.Generic;

namespace getcontainers
{
    class ArgumentParser
    {
        public static string ExtractArgumentValue(List<string> args, string flagname)
        {
            var index = args.IndexOf(flagname);
            if (index >= 0 && args.Count > index + 1)
            {
                var value = args[index + 1];
                args.RemoveRange(index, 2);
                return value;
            }
            else
            {
                return string.Empty;
            }
        }

        public static int ExtractArgumentInt(List<string> args, string flagname, int defaultValue)
        {
            var index = args.IndexOf(flagname);
            if (index >= 0 && args.Count > index + 1)
            {
                var value = args[index + 1];
                args.RemoveRange(index, 2);
                return int.TryParse(value, out int intValue) ? intValue : defaultValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public static string[] ExtractArgumentValues(List<string> args, string flagname)
        {
            var index = args.IndexOf(flagname);
            if (index >= 0 && args.Count > index + 1)
            {
                var values = args[index + 1].Split(',');
                args.RemoveRange(index, 2);
                return values;
            }
            else
            {
                return [];
            }
        }

        public static bool ExtractArgumentFlag(List<string> args, string flagname)
        {
            var index = args.IndexOf(flagname);
            if (index >= 0)
            {
                args.RemoveAt(index);
                return true;
            }
            else
            {
                return false;
            }
        }

        public static Dictionary<string, string> ExtractArgumentDictionary(List<string> args, string flagname)
        {
            var index = args.IndexOf(flagname);
            if (index >= 0 && args.Count > index + 1)
            {
                var values = args[index + 1].Split(',');
                args.RemoveRange(index, 2);
                Dictionary<string, string> dic = [];
                foreach (var value in values)
                {
                    index = value.IndexOf('=');
                    if (index >= 0)
                    {
                        dic.Add(value[..index], value[(index + 1)..]);
                    }
                }
                return dic;
            }
            else
            {
                return [];
            }
        }
    }
}
