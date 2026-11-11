using Microsoft.EntityFrameworkCore;
using Ordahh.Api.Models;

namespace Ordahh.Api.Data;

public class AppDBContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Burgers" },
            new Category { Id = 2, Name = "Sides" },
            new Category { Id = 3, Name = "Drinks" }
        );

        modelBuilder.Entity<MenuItem>().HasData(
            new MenuItem { Id = 1, Name = "Classic Burger", Description = "Beef patty, lettuce, tomato", Price = 8.99m, CategoryId = 1 },
            new MenuItem { Id = 2, Name = "Cheeseburger", Description = "Classic with cheddar", Price = 9.99m, CategoryId = 1 },
            new MenuItem { Id = 3, Name = "Fries", Description = "Crispy and salted", Price = 3.99m, CategoryId = 2},
            new MenuItem { Id = 4, Name = "Onion Rings", Price = 4.49m, CategoryId = 2 },
            new MenuItem { Id = 5, Name = "Cola", Price = 2.49m, CategoryId = 3 },
            new MenuItem { Id = 6, Name = "Lemonade", Price = 2.99m, CategoryId = 3 }
        );

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.MenuItem)
            .WithMany()
            .HasForeignKey(oi => oi.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    public AppDBContext(DbContextOptions<AppDBContext> options) : base(options)
    {
    }
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
}