using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Exceptions;
using k8s.Models;

namespace getcontainers
{
    public class Pod
    {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Cluster { get; set; } = string.Empty;
        public Dictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
        public string[] Images { get; set; } = new string[] { };
        public Container[] Containers { get; set; } = new Container[] { };
        public Status Status { get; set; } = new Status { };
    }

    public class Container
    {
        public string[] Args { get; set; } = new string[] { };
        public string[] Command { get; set; } = new string[] { };
        public string Image { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string WorkingDir { get; set; } = string.Empty;
    }

    public class Status
    {
        public ContainerStatus[] ContainerStatuses { get; set; } = new ContainerStatus[] { };
        public string Message { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = new DateTime();
    }

    public class ContainerStatus
    {
        public string ContainerID { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string ImageID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Ready { get; set; } = false;
        public int RestartCount { get; set; } = 0;
        public bool Started { get; set; } = false;
        public State State { get; set; } = new State();
    }

    public class State
    {
        public StateRunning StateRunning { get; set; } = new StateRunning();
        public StateTerminated StateTerminated { get; set; } = new StateTerminated();
        public StateWaiting StateWaiting { get; set; } = new StateWaiting();
    }

    public class StateRunning
    {
        public DateTime StartedAt { get; set; } = new DateTime();
    }

    public class StateTerminated
    {
        public string ContainerID { get; set; } = string.Empty;
        public int ExitCode { get; set; } = 0;
        public DateTime FinishedAt { get; set; } = new DateTime();
        public string Message { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = new DateTime();
    }

    public class StateWaiting
    {
        public string Message { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    class TableRow
    {
        public string Name { get; set; } = string.Empty;
        public string[][] Data { get; set; } = { };
        public bool Different { get; set; } = false;
    }

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var parsedArguments = args.ToList();

            var expandVersions = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-a");
            var showOnlyDifferent = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-d");
            var outputHtmlFile = ArgumentParser.ExtractArgumentValue(parsedArguments, "-h");
            var treatMissingAsEqual = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-m");
            var includeOther = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-o");
            var showNamespaces = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-s");
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
                    "getcontainers 0.006 gamma - Shows containers for multiple environments in a table.\n" +
                    "\n" +
                    "Usage: getcontainers <env1,env2,...> [-a] [-d] [-h file] [-m] [-o] [-s] [-t 123] [-v]\n" +
                    "  [-i container1,container2,...] [-ic cluster1,cluster2,...] [-in namespace1,namespace2,...]\n" +
                    "  [-x container1,container2,...] [-xc cluster1,cluster2,...] [-xn namespace1,namespace2,...]\n" +
                    "\n" +
                    "Group clusters into sorted environments, using substring of cluster name. Non-matching clusters can be grouped into \"other\".\n" +
                    "\n" +
                    "-a:  Expand multi version containers.\n" +
                    "-d:  Show only containers having different versions.\n" +
                    "-h:  Output to html file.\n" +
                    "-m:  Treat missing environments as equal when comparing diff.\n" +
                    "-o:  Group non-included environments in an \"other\" environment.\n" +
                    "-s:  Show namespaces.\n" +
                    "-t:  Timeout in seconds (10s default).\n" +
                    "-v:  Use version label instead of container version.\n" +
                    "-i:  Include containers, using substring of container name.\n" +
                    "-ic: Include clusters, using substring of cluster name.\n" +
                    "-in: Include namespaces, using substring of namespace name.\n" +
                    "-x:  Exclude containers, using substring of container name.\n" +
                    "-xc: Exclude clusters, using substring of cluster name.\n" +
                    "-xn: Exclude namespaces, using substring of namespace name.");
                return 1;
            }

            var environments = parsedArguments[0].Split(',');

            var pods = (await GetAllPods(includeClusters, excludeClusters, timeoutSeconds))
                .Where(p => includeNamespaces.Length == 0 || includeNamespaces.Any(n => p.Namespace.Contains(n, StringComparison.OrdinalIgnoreCase)))
                .Where(p => !excludeNamespaces.Any(n => p.Namespace.Contains(n, StringComparison.OrdinalIgnoreCase)))
                .Select(p => IncludeContainers(p, includeContainers))
                .Select(p => ExcludeContainers(p, excludeContainers))
                .ToArray();

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
                pod.Containers = pod.Containers.Where(c => includeContainers.Any(ec => c.Name.Contains(ec, StringComparison.OrdinalIgnoreCase))).ToArray();
            }
            return pod;
        }

        static Pod ExcludeContainers(Pod pod, string[] excludeContainers)
        {
            pod.Containers = pod.Containers.Where(c => !excludeContainers.Any(ec => c.Name.Contains(ec, StringComparison.OrdinalIgnoreCase))).ToArray();
            return pod;
        }

        static void ShowPods(Pod[] pods, string[] environments, bool showOnlyDifferent, bool expandVersions, bool treatMissingAsEqual, bool showNamespaces, bool includeOther, bool useLabelVersion, string outputHtmlFile)
        {
            var actualEnvironments = GetActualEnvironments(pods, environments, includeOther);
            var environmentmap = GetClusterMap(pods, environments);

            var containers = pods.SelectMany(p => p.Containers).Select(c => c.Name).Distinct().OrderBy(n => n).ToArray();
            var rows = new TableRow[containers.Length + 1];

            rows[0] = new TableRow();
            rows[0].Name = "Container";
            rows[0].Data = new string[actualEnvironments.Length][];
            rows[0].Different = false;
            for (int col = 0; col < actualEnvironments.Length; col++)
            {
                rows[0].Data[col] = new[] { actualEnvironments[col] };
            }
            for (var row = 0; row < containers.Length; row++)
            {
                rows[row + 1] = new TableRow();
                if (showNamespaces)
                {
                    rows[row + 1].Name = $"{containers[row]} ({string.Join(", ", pods.SelectMany(p => p.Containers.Where(c => c.Name == containers[row]).Select(c => p.Namespace)).Distinct().OrderBy(n => n))})";
                }
                else
                {
                    rows[row + 1].Name = containers[row];
                }
                rows[row + 1].Data = new string[actualEnvironments.Length][];
                for (int col = 0; col < actualEnvironments.Length; col++)
                {
                    string container = containers[row];
                    string environment = actualEnvironments[col];
                    rows[row + 1].Data[col] = GetContainerVersions(pods, container, environment, actualEnvironments, useLabelVersion);
                }
                rows[row + 1].Different = ContainsDifferent(rows[row + 1].Data, treatMissingAsEqual);
            }

            if (outputHtmlFile != string.Empty)
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
            if (data.Length == 0)
            {
                return false;
            }

            if (!treatMissingAsEqual)
            {
                return data.Skip(1).Any(a => !Enumerable.SequenceEqual(data[0], a));
            }

            var empty = new string[] { };

            string[] firstValue = empty;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].Length > 0)
                {
                    firstValue = data[i];
                    break;
                }
            }
            if (firstValue == empty)
            {
                return false;
            }

            return data.Skip(1).Any(a => a.Length > 0 && !Enumerable.SequenceEqual(data[0], a));
        }

        class SortableVersion
        {
            public string[] Version { get; set; } = new string[] { };
        }

        static string[] GetContainerVersions(Pod[] pods, string container, string environment, string[] actualEnvironments, bool useLabelVersion)
        {
            var versions = new List<string>();

            bool found = false;

            foreach (var pod in pods)
            {
                if (pod.Containers.Any(c => c.Name == container))
                {
                    if ((environment == "other" && !actualEnvironments.Where(e => e != "other").Any(ee => pod.Cluster.Contains(ee)))
                        ||
                        (environment != "other" && pod.Cluster.Contains(environment)))
                    {
                        var containers = pod.Containers.Where(c => c.Name == container).ToArray();
                        if (useLabelVersion)
                        {
                            if (pod.Labels.ContainsKey("version"))
                            {
                                var version = pod.Labels["version"];
                                versions.Add(version);
                            }
                        }
                        else
                        {
                            foreach (var c in containers)
                            {
                                var i = c.Image.IndexOf(':');
                                var version = i >= 0 ? c.Image.Substring(i + 1) : c.Image;
                                if (version.StartsWith('v'))
                                {
                                    version = version.Substring(1);
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

            var versionsArray = versions.Distinct().ToArray();

            Array.Sort(versionsArray, CompareVersions);

            return versionsArray;
        }

        static int CompareVersions(string version1, string version2)
        {
            int result = Compare(version1, version2);
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
            string[] v1 = version1.Split(separators);
            string[] v2 = version2.Split(separators);
            int elements = Math.Min(v1.Length, v2.Length);
            for (int i = 0; i < elements; i++)
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

                int result = string.Compare(v1[i], v2[i], StringComparison.OrdinalIgnoreCase);
                if (result < 0)
                {
                    return -1;
                }
                if (result > 0)
                {
                    return 1;
                }
            }

            if (v1.Length == v2.Length)
            {
                return 0;
            }

            return v1.Length < v2.Length ? -1 : 1;
        }

        static string[] GetActualEnvironments(Pod[] pods, string[] environments, bool includeOther)
        {
            var actual = environments.Where(e => pods.Any(p => p.Cluster.Contains(e))).ToList();

            if (includeOther)
            {
                if (pods.Any(p => !environments.Any(e => p.Cluster.Contains(e))))
                {
                    actual.Add("other");
                }
            }

            return actual.ToArray();
        }

        static Dictionary<string, string> GetClusterMap(Pod[] pods, string[] environments)
        {
            var map = new Dictionary<string, string>();

            foreach (var pod in pods)
            {
                var matches = environments.Where(e => pod.Cluster.Contains(e)).ToArray();

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

            string formatStringMulti = "<<< {0} >>>";

            if (!expandVersions)
            {
                foreach (var row in rows)
                {
                    for (int col = 0; col < row.Data.Length; col++)
                    {
                        if (row.Data[col].Length > 1)
                        {
                            row.Data[col] = new[] { string.Format(formatStringMulti, row.Data[col].Length) };
                        }
                    }
                }
            }

            string separator = new string(' ', 2);
            int[] maxwidths = GetMaxWidths(rows, separator, Console.WindowWidth, formatStringMulti);
            bool headerRow = true;

            foreach (var row in rows)
            {
                var output = new StringBuilder();

                output.AppendFormat("{0,-" + maxwidths[0] + "}", row.Name);

                for (int col = 0; col < row.Data.Length; col++)
                {
                    var value = string.Join(", ", row.Data[col]);
                    if (value.Length > maxwidths[col + 1])
                    {
                        value = string.Format(formatStringMulti, row.Data[col].Length);
                    }
                    output.AppendFormat("{0}{1,-" + maxwidths[col + 1] + "}", separator, value);
                }

                string textrow = output.ToString().TrimEnd();

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

            string formatStringMulti = "<<< {0} >>>";

            bool headerRow = true;

            var output = new StringBuilder();

            output.AppendLine(
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

                string colorAttribute = string.Empty;

                if (!showOnlyDifferent)
                {
                    colorAttribute = row.Different ? " class='diff'" : " class='nodiff'";
                }

                output.Append("<tr>");

                output.Append(headerRow ? $"<th>{row.Name}</th>" : $"<td{colorAttribute}>{row.Name}</td>");

                foreach (var values in row.Data)
                {
                    if (expandVersions)
                    {
                        var value = string.Join("<br/>", values);
                        output.Append(headerRow ? $"<th>{value}</th>" : $"<td{colorAttribute}>{value}</td>");
                    }
                    else
                    {
                        if (values.Length > 1)
                        {
                            string titleAttribute = $" title='{string.Join("\n", values)}'";
                            var value = string.Format(formatStringMulti, values.Length);
                            output.Append(headerRow ? $"<th>{value}</th>" : $"<td{colorAttribute}{titleAttribute}>{value}</td>");
                        }
                        else if (values.Length == 1)
                        {
                            output.Append(headerRow ? $"<th>{values[0]}</th>" : $"<td{colorAttribute}>{values[0]}</td>");
                        }
                        else
                        {
                            output.Append(headerRow ? "<th></th>" : "<td></td>");
                        }
                    }
                }

                output.AppendLine("</tr>");

                if (headerRow)
                {
                    headerRow = false;
                }
            }

            output.AppendLine($"</table></body></html>");

            Console.WriteLine($"Saving html file: '{filename}'");
            File.WriteAllText(filename, output.ToString());
        }

        static int[] GetMaxWidths(TableRow[] rows, string separator, int consolewidth, string formatStringMulti)
        {
            if (rows.Length == 0)
            {
                return new int[] { };
            }

            int[] maxwidths = new int[rows[0].Data.Length + 1];

            for (var row = 0; row < rows.Length; row++)
            {
                int length = rows[row].Name.Length;
                if (row == 0 || length > maxwidths[0])
                {
                    maxwidths[0] = length;
                }

                for (var col = 0; col < rows[row].Data.Length; col++)
                {
                    length = (string.Join(", ", rows[row].Data[col])).Length;
                    if (row == 0 || length > maxwidths[col + 1])
                    {
                        maxwidths[col + 1] = length;
                    }
                }
            }

            int max = 0;
            if (maxwidths.Length > 1)
            {
                max = maxwidths.Skip(1).Max();
            }
            while (max > 1 && maxwidths.Sum() + separator.Length * rows[0].Data.Length > consolewidth)
            {
                for (int col = 1; col < maxwidths.Length; col++)
                {
                    if (maxwidths[col] >= max)
                    {
                        int newmax = rows.Max(r =>
                        {
                            string value = string.Join(", ", r.Data[col - 1]);
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
            var config = KubernetesClientConfiguration.LoadKubeConfig();
            var clusters = new List<string>();

            if (includeClusters.Length == 0)
            {
                foreach (var context in config.Contexts.OrderBy(c => c.Name))
                {
                    clusters.Add(context.Name);
                }
            }
            else
            {
                foreach (var context in config.Contexts.OrderBy(c => c.Name))
                {
                    if (includeClusters.Any(i => context.Name.Contains(i)))
                    {
                        Console.WriteLine($"Including cluster: '{context.Name}'");
                        clusters.Add(context.Name);
                    }
                }
            }

            for (int i = 0; i < clusters.Count;)
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

            var allpods = new List<Task<List<Pod>>>();

            foreach (var cluster in clusters)
            {
                KubernetesClientConfiguration clientConfig;
                try
                {
                    clientConfig = KubernetesClientConfiguration.BuildConfigFromConfigObject(config, cluster);
                }
                catch (KubeConfigException ex)
                {
                    Console.WriteLine($"Ignoring cluster (invalid config): '{cluster}': {ex.Message}");
                    continue;
                }

                Console.WriteLine($"Connecting to: {clientConfig.Host} ({cluster})");
                IKubernetes client = new Kubernetes(clientConfig);

                allpods.Add(GetPods(client, cluster, timeoutSeconds));
            }

            await Task.WhenAll(allpods);

            return allpods.SelectMany(t => t.Result).ToList();
        }

        static async Task<List<Pod>> GetPods(IKubernetes client, string clusterName, int timeoutSeconds)
        {
            V1PodList pods;
            var newList = new List<Pod>();
            try
            {
                var task = client.CoreV1.ListPodForAllNamespacesAsync();
                if (await Task.WhenAny(task, Task.Delay(timeoutSeconds * 1000)) == task)
                {
                    pods = await task;
                }
                else
                {
                    Console.WriteLine($"Ignoring cluster: '{clusterName}': Timeout.");
                    return newList;
                }
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is HttpOperationException || ex is HttpRequestException)
            {
                Console.WriteLine($"Ignoring cluster: '{clusterName}': {ex.Message}");
                return newList;
            }

            foreach (var pod in pods.Items)
            {
                var newPod = new Pod();
                newPod.Name = pod.Metadata.Name;
                newPod.Namespace = pod.Metadata.NamespaceProperty;
                newPod.Cluster = clusterName;
                newPod.Annotations = pod.Annotations()?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>();
                newPod.Labels = pod.Labels()?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>();
                newPod.Status = new Status
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
                    })?.ToArray() ?? new ContainerStatus[] { }
                };
                newPod.Containers = pod.Spec.Containers.Select(c => new Container
                {
                    Args = c.Args?.Select(a => a)?.ToArray() ?? new string[] { },
                    Command = c.Command?.Select(c => c)?.ToArray() ?? new string[] { },
                    Image = c.Image,
                    Name = c.Name,
                    WorkingDir = c.WorkingDir
                }).ToArray();

                newList.Add(newPod);
            }

            return newList;
        }
    }
}
