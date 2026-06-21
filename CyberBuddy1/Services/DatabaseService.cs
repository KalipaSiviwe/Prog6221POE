using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CyberBuddy1.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            _connectionString = config.GetConnectionString("CyberBuddyDb")
                ?? throw new InvalidOperationException("Connection string 'CyberBuddyDb' not found in appsettings.json.");
        }

        public async Task<SqlConnection> OpenConnectionAsync()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            return conn;
        }

        public bool TestConnection(out string error)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}