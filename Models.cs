using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

class KubeConfig
{
    public KubeConfigCluster[] Clusters { get; set; } = [];
    public KubeConfigContext[] Contexts { get; set; } = [];
    public KubeConfigUser[] Users { get; set; } = [];
}

class KubeConfigCluster
{
    public string Cacert { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

class KubeConfigContext
{
    public string Cluster { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

class KubeConfigUser
{
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

class K8sPods
{
    [JsonPropertyName("items")]
    public K8sPod[] Items { get; set; } = [];
}

class K8sPod
{
    //[JsonPropertyName("name")]
    //public string Name { get; set; } = string.Empty;
    //[JsonPropertyName("namespace")]
    //public string Namespace { get; set; } = string.Empty;
    //public string Cluster { get; set; } = string.Empty;
    [JsonPropertyName("annotations")]
    public Dictionary<string, string> Annotations { get; set; } = [];
    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = [];
    //[JsonPropertyName("images")]
    //public string[] Images { get; set; } = [];
    //[JsonPropertyName("containers")]
    //public Container[] Containers { get; set; } = [];
    [JsonPropertyName("status")]
    public Status Status { get; set; } = new();
    [JsonPropertyName("metadata")]
    public K8sPodMetadata Metadata { get; set; } = new();
    [JsonPropertyName("spec")]
    public K8sSpec Spec { get; set; } = new();
}

class K8sPodMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("namespaceproperty")]
    public string NamespaceProperty { get; set; } = string.Empty;
}

class K8sSpec
{
    //[JsonPropertyName("nodename")]
    //public string NodeName { get; set; } = string.Empty;
    [JsonPropertyName("containers")]
    public K8sContainer[] Containers { get; set; } = [];
}

class K8sContainer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;
    [JsonPropertyName("args")]
    public string[] Args { get; set; } = [];
    [JsonPropertyName("command")]
    public string[] Command { get; set; } = [];
    [JsonPropertyName("workingdir")]
    public string WorkingDir { get; set; } = string.Empty;
}

class Pod
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Cluster { get; set; } = string.Empty;
    public Dictionary<string, string> Annotations { get; set; } = [];
    public Dictionary<string, string> Labels { get; set; } = [];
    public string[] Images { get; set; } = [];
    public Container[] Containers { get; set; } = [];
    public Status Status { get; set; } = new();
}

class Container
{
    public string[] Args { get; set; } = [];
    public string[] Command { get; set; } = [];
    public string Image { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WorkingDir { get; set; } = string.Empty;
}

class Status
{
    public ContainerStatus[] ContainerStatuses { get; set; } = [];
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
}

class ContainerStatus
{
    public string ContainerID { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string ImageID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Ready { get; set; }
    public int RestartCount { get; set; }
    public bool? Started { get; set; }
    public State State { get; set; } = new();
}

class State
{
    public StateRunning StateRunning { get; set; } = new();
    public StateTerminated StateTerminated { get; set; } = new();
    public StateWaiting StateWaiting { get; set; } = new();
    public RunningK8s? Running { get; set; }
    public TerminatedK8s Terminated { get; set; } = new();
    public WaitingK8s Waiting { get; set; } = new();
}

class RunningK8s
{
    public DateTime? StartedAt { get; set; }
}

class TerminatedK8s
{
    public string ContainerID { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public DateTime FinishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}

class WaitingK8s
{
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

class StateRunning
{
    public DateTime StartedAt { get; set; }
}

class StateTerminated
{
    public string ContainerID { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public DateTime FinishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}

class StateWaiting
{
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

class TableRow
{
    public string Name { get; set; } = string.Empty;
    public string[][] Data { get; set; } = [];
    public bool Different { get; set; }
}
