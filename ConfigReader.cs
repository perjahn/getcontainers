using System.Collections.Generic;
using System.IO;

class TreeNode
{
    public string Row { get; set; } = string.Empty;
    public List<TreeNode> Children { get; set; } = [];
}

class ConfigReader
{
    public static KubeConfig ReadConfig(string filename)
    {
        var rows = File.ReadAllLines(filename);
        var root = ParseRows(rows);
        //OutputTree(root, 0);
        TrimQuotes(root);
        var config = FlattenTree(root);

        return config;
    }

    /*
    static void OutputTree(TreeNode root, int indent)
    {
        System.Console.WriteLine($"{new string('_', indent)}'{root.Row}'");
        foreach (var child in root.Children)
        {
            OutputTree(child, indent + 2);
        }
    }
    */

    static TreeNode ParseRows(string[] rows)
    {
        TreeNode root = new();
        List<TreeNode> parsedNodes = [];

        foreach (var row in rows)
        {
            if (row.Trim().Length == 0)
            {
                continue;
            }

            var indent = LeadingSpaces(row);

            TreeNode? parent = null;
            for (var j = parsedNodes.Count - 1; j >= 0; j--)
            {
                var prevIndent = LeadingSpaces(parsedNodes[j].Row);
                if (prevIndent < indent || (prevIndent == indent && row.TrimStart().StartsWith('-') && !parsedNodes[j].Row.TrimStart().StartsWith('-')))
                {
                    parent = parsedNodes[j];
                    break;
                }
            }

            if (parent == null)
            {
                TreeNode node = new() { Row = row };
                root.Children.Add(node);
                parsedNodes.Add(node);
            }
            else
            {
                if (row.TrimStart().StartsWith('-'))
                {
                    TreeNode node = new() { Row = row[..(indent + 1)] };
                    parent.Children.Add(node);
                    parsedNodes.Add(node);

                    TreeNode n2 = new() { Row = $"{row[0..indent]} {row[(indent + 1)..]}" };
                    node.Children.Add(n2);
                    parsedNodes.Add(n2);
                }
                else
                {
                    TreeNode node = new() { Row = row };
                    parent.Children.Add(node);
                    parsedNodes.Add(node);
                }
            }
        }

        return root;
    }

    static int LeadingSpaces(string row)
    {
        var count = 0;
        foreach (var c in row)
        {
            if (char.IsWhiteSpace(c))
            {
                count++;
            }
            else
            {
                break;
            }
        }
        return count;
    }

    static void TrimQuotes(TreeNode root)
    {
        var index = root.Row.IndexOf(':');
        if (index >= 0)
        {
            var startIndex = root.Row.IndexOf('"', index + 1);
            if (startIndex >= 0)
            {
                var endIndex = root.Row.LastIndexOf('"', startIndex + 1);
                if (endIndex >= 0)
                {
                    root.Row = $"{root.Row[..startIndex]}{root.Row[startIndex..].Trim('"')}";
                }
            }
        }

        foreach (var node in root.Children)
        {
            TrimQuotes(node);
        }
    }

    static KubeConfig FlattenTree(TreeNode root)
    {
        KubeConfig config = new();

        foreach (var node1 in root.Children)
        {
            if (node1.Row.TrimStart() == "clusters:")
            {
                List<KubeConfigCluster> clusters = [];
                foreach (var node2 in node1.Children)
                {
                    if (node2.Row.TrimStart().StartsWith('-'))
                    {
                        KubeConfigCluster cluster = new();
                        foreach (var node3 in node2.Children)
                        {
                            if (node3.Row.TrimStart() == "cluster:")
                            {
                                foreach (var node4 in node3.Children)
                                {
                                    if (node4.Row.TrimStart().StartsWith("certificate-authority-data:"))
                                    {
                                        cluster.Cacert = node4.Row.TrimStart()["certificate-authority-data:".Length..].TrimStart();
                                    }
                                    else if (node4.Row.TrimStart().StartsWith("server:"))
                                    {
                                        cluster.Server = node4.Row.TrimStart()["server:".Length..].TrimStart();
                                    }
                                }
                            }
                            else if (node3.Row.TrimStart().StartsWith("name:"))
                            {
                                cluster.Name = node3.Row.TrimStart()["name:".Length..].TrimStart();
                            }
                        }
                        clusters.Add(cluster);
                    }
                }
                config.Clusters = [.. clusters];
            }
            else if (node1.Row.TrimStart() == "contexts:")
            {
                List<KubeConfigContext> contexts = [];
                foreach (var node2 in node1.Children)
                {
                    if (node2.Row.StartsWith('-'))
                    {
                        KubeConfigContext context = new();
                        foreach (var node3 in node2.Children)
                        {
                            if (node3.Row.TrimStart() == "context:")
                            {
                                foreach (var node4 in node3.Children)
                                {
                                    if (node4.Row.TrimStart().StartsWith("cluster:"))
                                    {
                                        context.Cluster = node4.Row.TrimStart()["cluster:".Length..].TrimStart();
                                    }
                                    else if (node4.Row.TrimStart().StartsWith("user:"))
                                    {
                                        context.User = node4.Row.TrimStart()["user:".Length..].TrimStart();
                                    }
                                }
                            }
                            else if (node3.Row.TrimStart().StartsWith("name:"))
                            {
                                context.Name = node3.Row.TrimStart()["name:".Length..].TrimStart();
                            }
                        }
                        contexts.Add(context);
                    }
                }
                config.Contexts = [.. contexts];
            }
            else if (node1.Row.TrimStart() == "users:")
            {
                List<KubeConfigUser> users = [];
                foreach (var node2 in node1.Children)
                {
                    if (node2.Row.StartsWith('-'))
                    {
                        KubeConfigUser user = new();
                        foreach (var node3 in node2.Children)
                        {
                            if (node3.Row.TrimStart() == "user:")
                            {
                                foreach (var node4 in node3.Children)
                                {
                                    if (node4.Row.TrimStart().StartsWith("token:"))
                                    {
                                        user.Token = node4.Row.TrimStart()["token:".Length..].TrimStart();
                                    }
                                }
                            }
                            else if (node3.Row.TrimStart().StartsWith("name:"))
                            {
                                user.Name = node3.Row.TrimStart()["name:".Length..].TrimStart();
                            }
                        }
                        users.Add(user);
                    }
                }
                config.Users = [.. users];
            }
        }

        return config;
    }
}
