using System.Collections.Generic;

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
    public K8sPod[] Items { get; set; } = [];
}

class K8sPod
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Cluster { get; set; } = string.Empty;
    public Dictionary<string, string> Annotations { get; set; } = [];
    public Dictionary<string, string> Labels { get; set; } = [];
    public string[] Images { get; set; } = [];
    public Container[] Containers { get; set; } = [];
    public Status Status { get; set; } = new();
    public K8sPodsMetadata Metadata { get; set; } = new();
    public K8sSpec Spec { get; set; } = new();
}

class K8sPodsMetadata
{
    public string Name { get; set; } = string.Empty;
    public string NamespaceProperty { get; set; } = string.Empty;
}

class K8sSpec
{
    public string NodeName { get; set; } = string.Empty;
    public K8sContainer[] Containers { get; set; } = [];
}

class K8sContainer
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string[] Args { get; set; } = [];
    public string[] Command { get; set; } = [];
    public string WorkingDir { get; set; } = string.Empty;
}
