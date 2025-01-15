using Microsoft.EntityFrameworkCore;

namespace Data.Core.Models;

[PrimaryKey("CountryId", "FilmId")]
public class CountryFilm
{
    public int CountryId { get; set; }
    public int FilmId { get; set; }
}