using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

public class DataEditorDataContext : DbContext
{
    protected override void OnConfiguring
        (DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "db");
    }

    public new DbSet<Company> Companies { get; set; }
    public new DbSet<Country> Countries { get; set; }
    public new DbSet<Film> Films { get; set; } 
    public new DbSet<Language> Languages { get; set; } 
    public new DbSet<Origin> Origins { get; set; } 
    public new DbSet<Person> People { get; set; } 
}