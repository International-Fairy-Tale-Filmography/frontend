using Microsoft.EntityFrameworkCore;

namespace Data.Core.Models;

[PrimaryKey("CountryId", "FilmId")]
public class FilmCountry
{
    public int FilmId { get; set; }
    public int CountryId { get; set; }

}