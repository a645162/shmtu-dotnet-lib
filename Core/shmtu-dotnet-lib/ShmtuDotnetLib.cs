using System.Reflection;

namespace shmtu;

public static class ShmtuDotnetLib
{
    public static string Version => GetVersion();

    private static string GetVersion()
    {
        var name = Assembly.GetExecutingAssembly().GetName();
        return name.Version?.ToString() ?? "";
    }
}