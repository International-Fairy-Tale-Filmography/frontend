using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;

namespace Data.Core.Models;

public class Film
{
    
   
    //public int FilmId { get; set; }
    [Key]
    public Guid Guid { get; set; }
    public string Title { get; set; }
    public string OtherTitle { get; set; }
    public string Color { get; set; }
    public int? ReleaseYear { get; set; }
    public int? Duration { get; set; }
    public string Mode { get; set; }
    public string Comment { get; set; }
    public bool? Published { get; set; }

    public List<FilmLink> Links { get; set; } = new();
    public List<FilmCompany> Companies { get; set; } = new();
    public List<FilmCountry> Countries { get; set; } = new();
    public List<FilmLanguage> Languages { get; set; } = new();
    public List<FilmOrigin> Origins { get; set; } = new();
    public List<FilmPersonRole> PeopleRoles { get; set; } = new();
}