using Microsoft.EntityFrameworkCore;

namespace Data.Core.Models;

[PrimaryKey("CompanyId", "FilmId")]
public class CompanyFilm
{
    public int CompanyId { get; set; }
    public int FilmId { get; set; }
}