using Microsoft.EntityFrameworkCore;
using AzureSqlMfaSample.Entities;


namespace AzureSqlMfaSample.Infras;

public class AsiaFlowDBContext : DbContext
    {
        public DbSet<PTSyncForm> PTSyncForms { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=sea-asia-tube-sqlsrv.database.windows.net;Authentication=Active Directory Interactive; Encrypt=True; Database=AsiaFlowDB");
        }
    }
