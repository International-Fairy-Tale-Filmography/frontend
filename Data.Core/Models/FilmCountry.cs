using System.ComponentModel.DataAnnotations.Schema;
using CsvHelper.Configuration.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Data.Core.Models;

[PrimaryKey("FilmId", "CountryId")]
public class FilmCountry
{
    [Ignore]
    [ForeignKey("CountryId")]
    public virtual Country Country { get; set; }

    [Ignore]
    [ForeignKey("FilmId")]
    public virtual Film Film { get; set; }

    public int FilmId { get; set; }
    public int CountryId { get; set; }

}