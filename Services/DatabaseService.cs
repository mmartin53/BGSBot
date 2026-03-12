using BGSBot.Database;

namespace BGSBot.Services
{
    public class DatabaseService
    {
        public EDDNDatabaseContext NewDatabaseContext()
        {
            return new EDDNDatabaseContext("C:\\Discord\\BGSDatabase.db");
        }

        public DatabaseService()
        {
            var database = NewDatabaseContext();
            database.Database.EnsureCreated();
        }
    }
}
