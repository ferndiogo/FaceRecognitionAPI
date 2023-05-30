using Microsoft.EntityFrameworkCore;
using FaceRecognitionAPI.Models;

namespace FaceRecognitionAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // define table on the database

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Registry> Registries { get; set; }
        public DbSet<User> Users { get; set; }

    }
}