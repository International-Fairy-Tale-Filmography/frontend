using System.ComponentModel.DataAnnotations.Schema;
using CsvHelper.Configuration.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Data.Core.Models;

[PrimaryKey("FilmGuid", "CountryGuid")]
public class FilmCountry
{
    [Ignore]
    [ForeignKey("CountryGuid")]
    public virtual Country Country { get; set; }

    [Ignore]
    [ForeignKey("FilmGuid")]
    public virtual Film Film { get; set; }
    
    public Guid FilmGuid { get; set; }
    
    public Guid CountryGuid { get; set; }

}