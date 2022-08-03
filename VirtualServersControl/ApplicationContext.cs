using Microsoft.EntityFrameworkCore;

namespace VirtualServersControl
{
    public class ApplicationContext : DbContext
    {
        public DbSet<VirtualServer>? VirtualServers { get; set; } = null;
        public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options)
        {
            Database.EnsureCreated();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VirtualServer>().HasData(
                new VirtualServer { VirtualServerID = 1, CreateDateTime = DateTime.Now.AddMinutes(-40) },
                new VirtualServer { VirtualServerID = 2, CreateDateTime = DateTime.Now }
            );
        }


    }
}
