using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace getcontainers
{
    class ArgumentParser
    {
        public static string ExtractArgumentValue(List<string> args, string flagname)
        {
            int index = args.IndexOf(flagname);
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
            int index = args.IndexOf(flagname);
            if (index >= 0 && args.Count > index + 1)
            {
                var value = args[index + 1];
                args.RemoveRange(index, 2);
                if (int.TryParse(value, out int intValue))
                {
                    return intValue;
                }
                return defaultValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public static string[] ExtractArgumentValues(List<string> args, string flagname)
        {
            int index = args.IndexOf(flagname);
            if (index >= 0 && args.Count > index + 1)
            {
                var values = args[index + 1].Split(',');
                args.RemoveRange(index, 2);
                return values;
            }
            else
            {
                return new string[] { };
            }
        }

        public static bool ExtractArgumentFlag(List<string> args, string flagname)
        {
            int index = args.IndexOf(flagname);
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
            int index = args.IndexOf(flagname);
            if (index >= 0 && args.Count > index + 1)
            {
                var values = args[index + 1].Split(',');
                args.RemoveRange(index, 2);
                var dic = new Dictionary<string, string>();
                foreach (var value in values)
                {
                    index = value.IndexOf('=');
                    if (index >= 0)
                    {
                        dic.Add(value.Substring(0, index), value.Substring(index + 1));
                    }
                }
                return dic;
            }
            else
            {
                return new Dictionary<string, string>();
            }
        }
    }
}
