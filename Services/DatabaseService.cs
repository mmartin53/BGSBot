using BGSBot.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
