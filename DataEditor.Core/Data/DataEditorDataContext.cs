using Data.Core.Models;
using Microsoft.EntityFrameworkCore;
using Language = Data.Core.Models.Language;

public class DataEditorDataContext : DbContext
{
    public DataEditorDataContext()
    {
    }

    public DataEditorDataContext(DbContextOptions<DataEditorDataContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=data-editor.db");
        }
    }

    public DbSet<Company> Companies { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<Film> Films { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<Origin> Origins { get; set; }
    public DbSet<Person> People { get; set; }
    public DbSet<Role> Roles { get; set; }

    public DbSet<FilmLink> FilmLinks { get; set; }
    public DbSet<FilmCompany> FilmCompanies { get; set; }
    public DbSet<FilmOrigin> FilmOrigins { get; set; }
    public DbSet<FilmCountry> FilmCountries { get; set; }
    public DbSet<FilmLanguage> FilmLanguages { get; set; }
    public DbSet<FilmPersonRole> FilmPersonRoles { get; set; }

    public void DetachAllEntities()
    {
        var entries = ChangeTracker.Entries().ToList();
        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    public void ClearAllDbSets()
    {
        FilmPersonRoles.RemoveRange(FilmPersonRoles);
        FilmOrigins.RemoveRange(FilmOrigins);
        FilmLanguages.RemoveRange(FilmLanguages);
        FilmCountries.RemoveRange(FilmCountries);
        FilmCompanies.RemoveRange(FilmCompanies);
        FilmLinks.RemoveRange(FilmLinks);

        Films.RemoveRange(Films);
        People.RemoveRange(People);
        Roles.RemoveRange(Roles);
        Origins.RemoveRange(Origins);
        Languages.RemoveRange(Languages);
        Countries.RemoveRange(Countries);
        Companies.RemoveRange(Companies);

        SaveChanges();
    }
}
