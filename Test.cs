using System;

class Test
{
    public void RunTests()
    {
        TestConfigReader();
    }

    void TestConfigReader()
    {
        var config = ConfigReader.ReadConfig("~/.kube/config");

        Console.WriteLine("Clusters:");
        foreach (var cluster in config.Clusters)
        {
            Console.WriteLine($"{cluster.Name}: '{cluster.Cacert}' '{cluster.Server}'");
        }

        Console.WriteLine("Contexts:");
        foreach (var context in config.Contexts)
        {
            Console.WriteLine($"{context.Name}: '{context.Cluster}' '{context.User}'");
        }

        Console.WriteLine("Users:");
        foreach (var user in config.Users)
        {
            Console.WriteLine($"{user.Name}: '{user.Token}'");
        }
    }
}
