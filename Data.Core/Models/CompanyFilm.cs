using System.ComponentModel.DataAnnotations.Schema;
using CsvHelper.Configuration.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Data.Core.Models;

[PrimaryKey("CompanyId", "FilmId")]
public class CompanyFilm
{
    [Ignore]
    [ForeignKey("CompanyId")]
    public virtual Company Company { get; set; }

    [Ignore]
    [ForeignKey("FilmId")]
    public virtual Film Film { get; set; }
    public int FilmId { get; set; }
    public int CompanyId { get; set; }
   
}