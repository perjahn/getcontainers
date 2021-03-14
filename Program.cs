using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Exceptions;
using k8s.Models;
using Microsoft.Rest;

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

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var parsedArguments = args.ToList();

            var showOnlyDifferent = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-d");
            var treatMissingAsEqual = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-m");
            var includeOther = ArgumentParser.ExtractArgumentFlag(parsedArguments, "-o");
            var timeoutSeconds = ArgumentParser.ExtractArgumentInt(parsedArguments, "-t", 10);
            var excludeContainers = ArgumentParser.ExtractArgumentValues(parsedArguments, "-x");
            var excludeClusters = ArgumentParser.ExtractArgumentValues(parsedArguments, "-xc");
            var excludeNamespaces = ArgumentParser.ExtractArgumentValues(parsedArguments, "-xn");

            if (parsedArguments.Count != 1)
            {
                Console.WriteLine(
                    "getcontainers 0.003 gamma - Shows containers for multiple environments in a table.\n" +
                    "\n" +
                    "Usage: getcontainers <env1,env2,...> [-d] [-m] [-o] [-t 123] [-x container1,container2,...] [-xc cluster1,cluster2,...] [-xn namespace1,namespace2,...]\n" +
                    "\n" +
                    "Group clusters into sorted environments, using substring of cluster name. Non-matching clusters will be grouped into \"other\".\n" +
                    "\n" +
                    "-d:  Show only containers having different versions.\n" +
                    "-m:  Treat missing environments as equal when comparing diff.\n" +
                    "-o:  Group non-included environments in an \"other\" environment.\n" +
                    "-t:  Timeout in seconds (10s default).\n" +
                    "-x:  Exclude containers, using substring in container name.\n" +
                    "-xc: Exclude clusters, using substring in cluster name.\n" +
                    "-xn: Exclude namespaces, using substring in namespace name.");
                return 1;
            }

            var environments = parsedArguments[0].Split(',');

            var pods = (await GetAllPods(excludeClusters, timeoutSeconds))
                .Where(p => !excludeNamespaces.Any(n => p.Namespace.Contains(n, StringComparison.OrdinalIgnoreCase)))
                .Select(p => ExcludeContainers(p, excludeContainers))
                .ToArray();

            if (pods.Length == 0)
            {
                Console.WriteLine("No pods found!");
                return 1;
            }

            ShowPods(pods, environments, showOnlyDifferent, treatMissingAsEqual, includeOther);

            return 0;
        }

        private static Pod ExcludeContainers(Pod pod, string[] excludeContainers)
        {
            pod.Containers = pod.Containers.Where(c => !excludeContainers.Any(ec => c.Name.Contains(ec, StringComparison.OrdinalIgnoreCase))).ToArray();
            return pod;
        }

        static void ShowPods(Pod[] pods, string[] environments, bool showOnlyDifferent, bool treatMissingAsEqual, bool includeOther)
        {
            var actualEnvironments = GetActualEnvironments(pods, environments, includeOther);
            var environmentmap = GetClusterMap(pods, environments);

            var containers = pods.SelectMany(p => p.Containers).Select(c => c.Name).Distinct().OrderBy(n => n).ToArray();
            var columns = (new[] { "Container" }).Concat(actualEnvironments).ToArray();

            var table = new string[containers.Length + 1, columns.Length];

            for (var row = 0; row <= containers.Length; row++)
            {
                for (int col = 0; col < columns.Length; col++)
                {
                    if (row == 0)
                    {
                        table[0, col] = columns[col];
                    }
                    else
                    {
                        if (col == 0)
                        {
                            table[row, 0] = containers[row - 1];
                        }
                        else
                        {
                            string container = containers[row - 1];
                            string environment = actualEnvironments[col - 1];
                            string[] versions = GetContainerVersions(pods, container, environment, actualEnvironments);
                            string v = string.Join(", ", versions);
                            if (v.Length > 35)
                            {
                                v = $"<<< {versions.Length} >>>";
                            }
                            table[row, col] = v;
                        }
                    }
                }
            }

            ShowTable(table, showOnlyDifferent, treatMissingAsEqual);
        }

        static string[] GetContainerVersions(Pod[] pods, string container, string environment, string[] actualEnvironments)
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
                        if (pod.Labels.ContainsKey("version"))
                        {
                            var version = pod.Labels["version"];
                            versions.Add(version);
                        }
                        found = true;
                    }
                }
            }

            if (versions.Count == 0 && found)
            {
                versions.Add(".");
            }

            return versions.Distinct().OrderBy(v => v).ToArray();
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

        static void ShowTable(string[,] items, bool showOnlyDifferent, bool treatMissingAsEqual)
        {
            int[] maxwidths = GetMaxWidths(items);

            string separator = new string(' ', 2);

            int rowcount = items.GetLength(0);
            int colcount = items.GetLength(1);

            for (int row = 0; row < rowcount; row++)
            {
                bool diff = false;
                var output = new StringBuilder();

                string firstValue = GetFirstValue(items, row, treatMissingAsEqual);

                for (int col = 0; col < colcount; col++)
                {
                    if (col > 0)
                    {
                        if (!treatMissingAsEqual && firstValue != items[row, col])
                        {
                            diff = true;
                        }
                        if (treatMissingAsEqual && (items[row, col] != string.Empty && firstValue != items[row, col]))
                        {
                            diff = true;
                        }
                        output.Append(separator);
                    }
                    output.AppendFormat("{0,-" + maxwidths[col] + "}", items[row, col]);
                }

                if (diff)
                {
                    if (showOnlyDifferent)
                    {
                        Log(output.ToString().TrimEnd());
                    }
                    else
                    {
                        Log(output.ToString().TrimEnd(), ConsoleColor.Yellow);
                    }
                }
                else
                {
                    if (!showOnlyDifferent)
                    {
                        Log(output.ToString().TrimEnd(), ConsoleColor.Green);
                    }
                }
            }
        }

        private static string GetFirstValue(string[,] items, int row, bool treatMissingAsEqual)
        {
            if (!treatMissingAsEqual)
            {
                return items[row, 1];
            }

            for (int col = 1; col < items.GetLength(1); col++)
            {
                if (items[row, col] != string.Empty)
                {
                    return items[row, col];
                }
            }

            return string.Empty;
        }

        static int[] GetMaxWidths(string[,] items)
        {
            int rowcount = items.GetLength(0);
            int colcount = items.GetLength(1);
            int[] maxwidths = new int[colcount];

            for (var row = 0; row < rowcount; row++)
            {
                for (var col = 0; col < colcount; col++)
                {
                    if (row == 0)
                    {
                        maxwidths[col] = items[row, col].Length;
                    }
                    else
                    {
                        if (items[row, col].Length > maxwidths[col])
                        {
                            maxwidths[col] = items[row, col].Length;
                        }
                    }
                }
            }

            return maxwidths;
        }

        static void Log(string message)
        {
            Console.WriteLine(message);
        }

        static void Log(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }

        static async Task<List<Pod>> GetAllPods(string[] excludeClusters, int timeoutSeconds)
        {
            var config = KubernetesClientConfiguration.LoadKubeConfig();
            var clusters = new List<string>();

            foreach (var context in config.Contexts.OrderBy(c => c.Name))
            {
                if (excludeClusters.Any(e => context.Name.Contains(e)))
                {
                    Console.WriteLine($"Excluding cluster: '{context.Name}'");
                    continue;
                }
                clusters.Add(context.Name);
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
                var task = client.ListPodForAllNamespacesAsync();
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
