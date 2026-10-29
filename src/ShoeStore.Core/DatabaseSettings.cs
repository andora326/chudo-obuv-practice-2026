using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace ShoeStore.Core;

public static class DatabaseSettings
{
    public const string DefaultConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=ChudoObuvPractice;Integrated Security=True;Encrypt=False;Connect Timeout=30";

    public static string Load(string applicationDirectory)
    {
        var path = Path.Combine(applicationDirectory, "connection-string.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : DefaultConnectionString;
    }

    public static void EnsureDatabase(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        if (!Regex.IsMatch(database, @"\A[A-Za-z][A-Za-z0-9_]{0,100}\z"))
            throw new InvalidOperationException("Укажите имя базы латинскими буквами, цифрами и подчёркиваниями в connection-string.txt.");
        builder.InitialCatalog = "master";
        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "IF DB_ID(@name) IS NULL EXEC(N'CREATE DATABASE [" + database + "]');";
        command.Parameters.AddWithValue("@name", database);
        command.ExecuteNonQuery();
    }
}
