
namespace ShoeStore.Core;

public static class DatabaseSettings
{
    public const string DefaultConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=ChudoObuvPractice;Integrated Security=True;Encrypt=False;Connect Timeout=30";

    public static string Load(string applicationDirectory)
    {
        var path = Path.Combine(applicationDirectory, "connection-string.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : DefaultConnectionString;
    }

}
