using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

public class EfDataContext : DbContext
{
    protected override void OnConfiguring
        (DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "db");
    }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<Film> Films { get; set; } 
    public DbSet<Language> Languages { get; set; } 
    public DbSet<Origin> Origins { get; set; } 
    public DbSet<Person> People { get; set; } 
}