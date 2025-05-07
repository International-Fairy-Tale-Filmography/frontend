using System;
using System.Collections.Generic;
using System.Text;
using Data.Core.Models;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Language = Data.Core.Models.Language;

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
    public new DbSet<Role> Roles { get; set; } 

    public new DbSet<FilmLink> FilmLinks { get; set; } 
    public new DbSet<FilmCompany> FilmCompanies { get; set; } 
    public new DbSet<FilmOrigin> FilmOrigins { get; set; } 
    public new DbSet<FilmCountry> FilmCountries { get; set; } 
    public new DbSet<FilmLanguage> FilmLanguages { get; set; } 
    public new DbSet<FilmPersonRole> FilmPersonRoles { get; set; } 

    /// <summary>
    /// Detaches all tracked entities from the DbContext to avoid tracking conflicts.
    /// </summary>
    public void DetachAllEntities()
    {
        var entries = ChangeTracker.Entries().ToList();
        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Clears all DbSets by removing all entities and resetting their state.
    /// </summary>
    public void ClearAllDbSets()
    {
        Companies.RemoveRange(Companies);
        Countries.RemoveRange(Countries);
        Films.RemoveRange(Films);
        Languages.RemoveRange(Languages);
        Origins.RemoveRange(Origins);
        People.RemoveRange(People);
        Roles.RemoveRange(Roles);
        FilmLinks.RemoveRange(FilmLinks);
        FilmCompanies.RemoveRange(FilmCompanies);
        FilmOrigins.RemoveRange(FilmOrigins);
        FilmCountries.RemoveRange(FilmCountries);
        FilmLanguages.RemoveRange(FilmLanguages);
        FilmPersonRoles.RemoveRange(FilmPersonRoles);

        SaveChanges(); // Ensure changes are persisted
    }
}