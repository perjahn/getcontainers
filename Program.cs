using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        List<string> parsedArguments = [.. args];

        var expandVersions = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-a");
        var showOnlyDifferent = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-d");
        var outputHtmlFile = ArgumentParser.ExtractArgumentValue(parsedArguments, "-h");
        var treatMissingAsEqual = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-m");
        var showNamespaces = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-n");
        var includeOther = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-o");
        var timeoutSeconds = ArgumentParser.ExtractArgumentInt(parsedArguments, "-t", 10);
        var useLabelVersion = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-v");
        var includeContainers = ArgumentParser.ExtractArgumentValues(parsedArguments, "-i");
        var includeClusters = ArgumentParser.ExtractArgumentValues(parsedArguments, "-ic");
        var includeNamespaces = ArgumentParser.ExtractArgumentValues(parsedArguments, "-in");
        var excludeContainers = ArgumentParser.ExtractArgumentValues(parsedArguments, "-x");
        var excludeClusters = ArgumentParser.ExtractArgumentValues(parsedArguments, "-xc");
        var excludeNamespaces = ArgumentParser.ExtractArgumentValues(parsedArguments, "-xn");

        if (parsedArguments.Count != 1)
        {
            Console.WriteLine(
                "getcontainers 0.009 gamma - Shows containers for multiple environments in a table.\n" +
                "\n" +
                "Usage: getcontainers <env1,env2,...> [-a] [-d] [-h file] [-m] [-n] [-o] [-t 123] [-v]\n" +
                "  [-i container1,container2,...] [-ic cluster1,cluster2,...] [-in namespace1,namespace2,...]\n" +
                "  [-x container1,container2,...] [-xc cluster1,cluster2,...] [-xn namespace1,namespace2,...]\n" +
                "\n" +
                "Group clusters into sorted environments, using substring of cluster name. Non-matching clusters can be grouped into \"other\".\n" +
                "\n" +
                "-a:  Expand multi version containers.\n" +
                "-d:  Show only containers having different versions.\n" +
                "-h:  Output to html file.\n" +
                "-m:  Treat missing environments as equal when comparing diff.\n" +
                "-n:  Show namespaces.\n" +
                "-o:  Group non-included environments in an \"other\" environment.\n" +
                "-t:  Timeout in seconds (10s default).\n" +
                "-v:  Use version label of pod instead of container image version.\n" +
                "-i:  Include containers, using substring of container name.\n" +
                "-ic: Include clusters, using substring of cluster name.\n" +
                "-in: Include namespaces, using substring of namespace name.\n" +
                "-x:  Exclude containers, using substring of container name.\n" +
                "-xc: Exclude clusters, using substring of cluster name.\n" +
                "-xn: Exclude namespaces, using substring of namespace name.");
            return 1;
        }

        if (args.Length == 1 && args[0] == "test")
        {
            Test test = new();
            test.RunTests();
            return 0;
        }

        var environments = parsedArguments[0].Split(',');

        Pod[] pods = [.. (await GetAllPods(includeClusters, excludeClusters, timeoutSeconds))
            .Where(p => includeNamespaces.Length == 0 || includeNamespaces.Any(n => p.Namespace.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .Where(p => !excludeNamespaces.Any(n => p.Namespace.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .Select(p => IncludeContainers(p, includeContainers))
            .Select(p => ExcludeContainers(p, excludeContainers))];

        if (pods.Length == 0)
        {
            Console.WriteLine("No pods found!");
            return 1;
        }

        ShowPods(pods, environments, showOnlyDifferent, expandVersions, treatMissingAsEqual, showNamespaces, includeOther, useLabelVersion, outputHtmlFile);

        return 0;
    }

    static Pod IncludeContainers(Pod pod, string[] includeContainers)
    {
        if (includeContainers.Length > 0)
        {
            pod.Containers = [.. pod.Containers.Where(c => includeContainers.Any(ec => c.Name.Contains(ec, StringComparison.OrdinalIgnoreCase)))];
        }
        return pod;
    }

    static Pod ExcludeContainers(Pod pod, string[] excludeContainers)
    {
        pod.Containers = [.. pod.Containers.Where(c => !excludeContainers.Any(ec => c.Name.Contains(ec, StringComparison.OrdinalIgnoreCase)))];
        return pod;
    }

    static void ShowPods(Pod[] pods, string[] environments, bool showOnlyDifferent, bool expandVersions, bool treatMissingAsEqual, bool showNamespaces, bool includeOther, bool useLabelVersion, string outputHtmlFile)
    {
        var actualEnvironments = GetActualEnvironments(pods, environments, includeOther);
        var environmentmap = GetClusterMap(pods, environments);

        string[] containers = [.. pods.SelectMany(p => p.Containers).Select(c => c.Name).Distinct().OrderBy(n => n)];
        var rows = new TableRow[containers.Length + 1];

        rows[0] = new TableRow
        {
            Name = "Container",
            Data = new string[actualEnvironments.Length][],
            Different = false
        };
        for (var col = 0; col < actualEnvironments.Length; col++)
        {
            rows[0].Data[col] = [actualEnvironments[col]];
        }
        for (var row = 0; row < containers.Length; row++)
        {
            rows[row + 1] = new TableRow
            {
                Name = showNamespaces
                    ? $"{containers[row]} ({string.Join(", ", pods.SelectMany(p => p.Containers.Where(c => c.Name == containers[row]).Select(c => p.Namespace)).Distinct().OrderBy(n => n))})"
                    : containers[row],
                Data = new string[actualEnvironments.Length][]
            };
            for (var col = 0; col < actualEnvironments.Length; col++)
            {
                string container = containers[row];
                string environment = actualEnvironments[col];
                rows[row + 1].Data[col] = GetContainerVersions(pods, container, environment, actualEnvironments, useLabelVersion);
            }
            rows[row + 1].Different = ContainsDifferent(rows[row + 1].Data, treatMissingAsEqual);
        }

        if (outputHtmlFile.Length != 0)
        {
            SaveTableHtml(rows, showOnlyDifferent, expandVersions, outputHtmlFile);
        }
        else
        {
            ShowTable(rows, showOnlyDifferent, expandVersions);
        }
    }

    static bool ContainsDifferent(string[][] data, bool treatMissingAsEqual)
    {
        if (data.Length <= 1)
        {
            return false;
        }

        var firstValue = -1;
        for (var i = 0; i < data.Length; i++)
        {
            if (!treatMissingAsEqual || data[i].Length > 0)
            {
                if (firstValue >= 0)
                {
                    if (data[firstValue].Length != data[i].Length)
                    {
                        return true;
                    }

                    for (var j = 0; j < data[i].Length; j++)
                    {
                        if (data[firstValue][j] != data[i][j])
                        {
                            return true;
                        }
                    }
                }
                firstValue = i;
            }
        }

        return false;
    }

    static string[] GetContainerVersions(Pod[] pods, string container, string environment, string[] actualEnvironments, bool useLabelVersion)
    {
        List<string> versions = [];
        var found = false;

        foreach (var pod in pods)
        {
            if (pod.Containers.Any(c => c.Name == container))
            {
                if ((environment == "other" && !actualEnvironments.Where(e => e != "other").Any(pod.Cluster.Contains))
                    ||
                    (environment != "other" && pod.Cluster.Contains(environment)))
                {
                    Container[] containers = [.. pod.Containers.Where(c => c.Name == container)];
                    if (useLabelVersion)
                    {
                        if (pod.Labels.TryGetValue("version", out string? version))
                        {
                            versions.Add(version);
                        }
                    }
                    else
                    {
                        foreach (var c in containers)
                        {
                            var start = c.Image.IndexOf(':');
                            string version;
                            if (start >= 0)
                            {
                                var end = c.Image.IndexOf('@', start + 1);
                                version = end >= 0 ? c.Image.Substring(start + 1, end - start - 1) : c.Image[(start + 1)..];
                            }
                            else
                            {
                                version = c.Image;
                            }

                            if (version.StartsWith('v'))
                            {
                                version = version[1..];
                            }
                            versions.Add(version);
                        }
                    }

                    found = true;
                }
            }
        }

        if (versions.Count == 0 && found)
        {
            versions.Add(".");
        }

        string[] versionsArray = [.. versions.Distinct()];

        Array.Sort(versionsArray, CompareVersions);

        return versionsArray;
    }

    static int CompareVersions(string version1, string version2)
    {
        var result = Compare(version1, version2);
        return result;
    }

    /*
    Returns a signed integer that indicates the relative values of version1 and version2:
    - -1: version1 is less than version2
    - 0: version1 is equal to version2
    - 1: version1 is greater than version2
    */
    static int Compare(string version1, string version2)
    {
        var separators = new char[] { '.', '-' };
        var v1 = version1.Split(separators);
        var v2 = version2.Split(separators);
        var elements = Math.Min(v1.Length, v2.Length);
        for (var i = 0; i < elements; i++)
        {
            if (int.TryParse(v1[i], out int i1) && int.TryParse(v2[i], out int i2))
            {
                if (i1 < i2)
                {
                    return -1;
                }
                if (i1 > i2)
                {
                    return 1;
                }
                if (i1 == i2)
                {
                    continue;
                }
            }

            var result = string.Compare(v1[i], v2[i], StringComparison.OrdinalIgnoreCase);
            if (result < 0)
            {
                return -1;
            }
            if (result > 0)
            {
                return 1;
            }
        }

        return v1.Length == v2.Length ? 0 : v1.Length < v2.Length ? -1 : 1;
    }

    static string[] GetActualEnvironments(Pod[] pods, string[] environments, bool includeOther)
    {
        List<string> actual = [.. environments.Where(e => pods.Any(p => p.Cluster.Contains(e)))];

        if (includeOther)
        {
            if (pods.Any(p => !environments.Any(e => p.Cluster.Contains(e))))
            {
                actual.Add("other");
            }
        }

        return [.. actual];
    }

    static Dictionary<string, string> GetClusterMap(Pod[] pods, string[] environments)
    {
        var map = new Dictionary<string, string>();

        foreach (var pod in pods)
        {
            string[] matches = [.. environments.Where(pod.Cluster.Contains)];

            string environment;

            if (matches.Length >= 1)
            {
                if (matches.Length >= 2)
                {
                    Console.WriteLine($"Warning, too many matches for '{pod.Cluster}': '{string.Join("', '", matches)}'");
                }
                environment = matches[0];
            }
            else
            {
                environment = "other";
            }

            map[pod.Cluster] = environment;
        }

        return map;
    }

    static void ShowTable(TableRow[] rows, bool showOnlyDifferent, bool expandVersions)
    {
        if (rows.Length == 0)
        {
            return;
        }

        var formatStringMulti = "<<< {0} >>>";

        if (!expandVersions)
        {
            foreach (var row in rows)
            {
                for (var col = 0; col < row.Data.Length; col++)
                {
                    if (row.Data[col].Length > 1)
                    {
                        row.Data[col] = [string.Format(formatStringMulti, row.Data[col].Length)];
                    }
                }
            }
        }

        string separator = new(' ', 2);
        var maxwidths = GetMaxWidths(rows, separator, Console.WindowWidth, formatStringMulti);
        var headerRow = true;

        foreach (var row in rows)
        {
            StringBuilder output = new();

            _ = output.AppendFormat("{0,-" + maxwidths[0] + "}", row.Name);

            for (var col = 0; col < row.Data.Length; col++)
            {
                var value = string.Join(", ", row.Data[col]);
                if (value.Length > maxwidths[col + 1])
                {
                    value = string.Format(formatStringMulti, row.Data[col].Length);
                }
                _ = output.AppendFormat("{0}{1,-" + maxwidths[col + 1] + "}", separator, value);
            }

            var textrow = output.ToString().TrimEnd();

            if (headerRow)
            {
                Console.WriteLine(textrow);
                headerRow = false;
            }
            else
            {
                const string yellowAnsi = "\u001b[33m";
                const string greenAnsi = "\u001b[32m";
                const string resetAnsi = "\u001b[0m";

                if (row.Different)
                {
                    if (showOnlyDifferent)
                    {
                        Console.WriteLine(textrow);
                    }
                    else
                    {
                        Console.WriteLine($"{yellowAnsi}{textrow}{resetAnsi}");
                    }
                }
                else
                {
                    if (!showOnlyDifferent)
                    {
                        Console.WriteLine($"{greenAnsi}{textrow}{resetAnsi}");
                    }
                }
            }
        }
    }

    static void SaveTableHtml(TableRow[] rows, bool showOnlyDifferent, bool expandVersions, string filename)
    {
        if (rows.Length == 0)
        {
            return;
        }

        var formatStringMulti = "<<< {0} >>>";
        var headerRow = true;
        StringBuilder output = new();

        _ = output.AppendLine(
@"<html><head><style>
body, th, td {
  font-family: Arial, Helvetica, sans-serif
}
body {
  background-color: black;
}
table {
  border-collapse: collapse;
}
th, td {
  color: white;
  vertical-align: top;
  text-align: left;
  border: solid 1px white;
  white-space: nowrap;
}
.diff { color: #FFFF00; }
.nodiff { color: #00FF00; }
</style></head><body><table>");

        foreach (var row in rows)
        {
            if (!row.Different && showOnlyDifferent)
            {
                continue;
            }

            var colorAttribute = string.Empty;

            if (!showOnlyDifferent)
            {
                colorAttribute = row.Different ? " class='diff'" : " class='nodiff'";
            }

            _ = output.Append("<tr>");

            _ = output.Append(headerRow ? $"<th>{row.Name}</th>" : $"<td{colorAttribute}>{row.Name}</td>");

            foreach (var values in row.Data)
            {
                if (expandVersions)
                {
                    var value = string.Join("<br/>", values);
                    _ = output.Append(headerRow ? $"<th>{value}</th>" : $"<td{colorAttribute}>{value}</td>");
                }
                else
                {
                    if (values.Length > 1)
                    {
                        var titleAttribute = $" title='{string.Join('\n', values)}'";
                        var value = string.Format(formatStringMulti, values.Length);
                        _ = output.Append(headerRow ? $"<th>{value}</th>" : $"<td{colorAttribute}{titleAttribute}>{value}</td>");
                    }
                    else
                    {
                        _ = values.Length == 1
                            ? output.Append(headerRow ? $"<th>{values[0]}</th>" : $"<td{colorAttribute}>{values[0]}</td>")
                            : output.Append(headerRow ? "<th></th>" : "<td></td>");
                    }
                }
            }

            _ = output.AppendLine("</tr>");

            if (headerRow)
            {
                headerRow = false;
            }
        }

        _ = output.AppendLine($"</table></body></html>");

        Console.WriteLine($"Saving html file: '{filename}'");
        File.WriteAllText(filename, output.ToString());
    }

    static int[] GetMaxWidths(TableRow[] rows, string separator, int consolewidth, string formatStringMulti)
    {
        if (rows.Length == 0)
        {
            return [];
        }

        var maxwidths = new int[rows[0].Data.Length + 1];

        for (var row = 0; row < rows.Length; row++)
        {
            var length = rows[row].Name.Length;
            if (row == 0 || length > maxwidths[0])
            {
                maxwidths[0] = length;
            }

            for (var col = 0; col < rows[row].Data.Length; col++)
            {
                length = string.Join(", ", rows[row].Data[col]).Length;
                if (row == 0 || length > maxwidths[col + 1])
                {
                    maxwidths[col + 1] = length;
                }
            }
        }

        var max = 0;
        if (maxwidths.Length > 1)
        {
            max = maxwidths.Skip(1).Max();
        }
        while (max > 1 && maxwidths.Sum() + (separator.Length * rows[0].Data.Length) > consolewidth)
        {
            for (var col = 1; col < maxwidths.Length; col++)
            {
                if (maxwidths[col] >= max)
                {
                    var newmax = rows.Max(r =>
                    {
                        var value = string.Join(", ", r.Data[col - 1]);
                        if (value.Length >= max && r.Data[col - 1].Length > 1)
                        {
                            value = string.Format(formatStringMulti, r.Data[col - 1].Length);
                        }
                        return value.Length;
                    });

                    maxwidths[col] = newmax;
                }
            }
            max--;
        }

        return maxwidths;
    }

    static async Task<List<Pod>> GetAllPods(string[] includeClusters, string[] excludeClusters, int timeoutSeconds)
    {
        var homefolder = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        var filename = Path.Combine(homefolder, ".kube/config");
        if (!File.Exists(filename))
        {
            Console.WriteLine($"File not found: '{filename}'");
            return [];
        }

        var config = ConfigReader.ReadConfig(filename);

        List<string> clusters = [];

        if (includeClusters.Length == 0)
        {
            clusters = [.. config.Clusters.Select(c => c.Name).OrderBy(c => c)];
        }
        else
        {
            foreach (var cluster in config.Clusters.OrderBy(c => c.Name))
            {
                if (includeClusters.Any(c => c == cluster.Name))
                {
                    Console.WriteLine($"Including cluster: '{cluster.Name}'");
                    clusters.Add(cluster.Name);
                }
            }
        }

        for (var i = 0; i < clusters.Count;)
        {
            if (excludeClusters.Any(e => clusters[i] == e))
            {
                Console.WriteLine($"Excluding cluster: '{clusters[i]}'");
                clusters.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }

        //List<Task<List<Pod>>> allpods = [];
        List<Pod> allpods = [];

        foreach (var cluster in clusters)
        {
            Console.WriteLine($"Cluster: '{cluster}'");
            var server = config.Clusters.Single(c => c.Name == cluster).Server;
            if (!server.EndsWith('/'))
            {
                server += '/';
            }
            Console.WriteLine($"Connecting to: {server} ({cluster})");
            using HttpClient client = new() { BaseAddress = new(server) };

            var bearer = config.Users.Single(u => u.Name == config.Contexts.Single(c => c.Cluster == cluster).User).Token;
            //Console.WriteLine($"Bearer token: '{bearer}'");
            client.DefaultRequestHeaders.Authorization = new("Bearer", bearer);

            //allpods.Add(GetPods(client, cluster, timeoutSeconds));
            allpods.AddRange(await GetPods(client, cluster, timeoutSeconds));
        }

        //_ = await Task.WhenAll(allpods);

        //return [.. allpods.SelectMany(t => t.Result)];
        return allpods;
    }

    static async Task<List<Pod>> GetPods(HttpClient client, string clusterName, int timeoutSeconds)
    {
        K8sPods? pods;
        List<Pod> newList = [];

        //try
        //{
        var response = await client.GetAsync("api/v1/pods");
        var content = await response.Content.ReadAsStringAsync();
        pods = JsonSerializer.Deserialize<K8sPods>(content);
        if (timeoutSeconds < 0)
        {
            timeoutSeconds = 0;
        }
        /*
        var task = client.GetAsync("api/v1/pods");
        if (await Task.WhenAny(task, Task.Delay(timeoutSeconds * 1000)) == task)
        {
            var response = await task;
            _ = response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            pods = JsonSerializer.Deserialize<K8sPods>(content);
        }
        else
        {
            Console.WriteLine($"Ignoring cluster: '{clusterName}': Timeout.");
            return newList;
        }
        */
        //}
        //catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        //{
        //    Console.WriteLine($"Ignoring cluster: '{clusterName}': {ex}");
        //    return newList;
        //}
        if (pods == null)
        {
            return newList;
        }

        foreach (var pod in pods.Items)
        {
            var newPod = new Pod
            {
                Name = pod.Metadata.Name,
                Namespace = pod.Metadata.NamespaceProperty,
                Cluster = clusterName,
                Annotations = pod.Annotations?.ToDictionary(t => t.Key, t => t.Value) ?? [],
                Labels = pod.Labels?.ToDictionary(t => t.Key, t => t.Value) ?? [],
                Status = new Status
                {
                    Message = pod.Status.Message,
                    Reason = pod.Status.Reason,
                    StartTime = pod.Status.StartTime ?? new DateTime(),
                    ContainerStatuses = pod.Status.ContainerStatuses?.Select(cs => new ContainerStatus
                    {
                        ContainerID = cs.ContainerID,
                        Image = cs.Image,
                        ImageID = cs.ImageID,
                        Name = cs.Name,
                        Ready = cs.Ready,
                        RestartCount = cs.RestartCount,
                        Started = cs.Started ?? false,
                        State = new State
                        {
                            StateRunning = new StateRunning
                            {
                                StartedAt = cs.State.Running?.StartedAt ?? new DateTime()
                            },
                            StateTerminated = new StateTerminated
                            {
                                ContainerID = cs.State.Terminated?.ContainerID ?? string.Empty,
                                ExitCode = cs.State.Terminated?.ExitCode ?? 0,
                                FinishedAt = cs.State.Terminated?.FinishedAt ?? new DateTime(),
                                Message = cs.State.Terminated?.Message ?? string.Empty,
                                Reason = cs.State.Terminated?.Reason ?? string.Empty,
                                StartedAt = cs.State.Terminated?.StartedAt ?? new DateTime()
                            },
                            StateWaiting = new StateWaiting
                            {
                                Message = cs.State.Waiting?.Message ?? string.Empty,
                                Reason = cs.State.Waiting?.Reason ?? string.Empty
                            }
                        }
                    })?.ToArray() ?? []
                },
                Containers = [.. pod.Spec.Containers.Select(c => new Container
                    {
                        Args = c.Args?.Select(a => a)?.ToArray() ?? [],
                        Command = c.Command?.Select(c => c)?.ToArray() ?? [],
                        Image = c.Image,
                        Name = c.Name,
                        WorkingDir = c.WorkingDir
                    })]
            };

            newList.Add(newPod);
        }

        return newList;
    }
}
