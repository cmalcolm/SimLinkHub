using Microsoft.EntityFrameworkCore;
using SimLinkHub.Models;

namespace SimLinkHub.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<DeviceConfig> Configs { get; set; }
        public DbSet<ArduinoDevice> Arduinos { get; set; }
        public DbSet<SimInstrument> Instruments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // This ensures the DB is created in the same folder as your .exe
            options.UseSqlite("Data Source=simlink.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Force the table name to match exactly what the error is looking for
            modelBuilder.Entity<DeviceConfig>().ToTable("Configs");

            // Set the Primary Key explicitly just in case
            modelBuilder.Entity<DeviceConfig>().HasKey(c => c.Id);
        }
        public AppDbContext()
        {
            // This will create the .db file and all tables (Arduinos, Instruments, etc.)
            // if they don't already exist.
            Database.EnsureCreated();
        }
    }
}