using Npgsql;

namespace Volterp.Tools;

// The database the command is about to write to, said out loud before it
// writes: nobody should create a tenant in a database they did not mean.
public static class Target
{
    public static string Describe(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        return $"{builder.Host}/{builder.Database}";
    }
}
