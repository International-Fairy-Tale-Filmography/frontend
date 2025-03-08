using System.ComponentModel.DataAnnotations.Schema;
using CsvHelper.Configuration.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Data.Core.Models;

[PrimaryKey("FilmGuid", "CompanyGuid")]
public class FilmCompany
{
    [Ignore]
    [ForeignKey("CompanyGuid")]
    public virtual Company Company { get; set; }

    [Ignore]
    [ForeignKey("FilmGuid")]
    public virtual Film Film { get; set; }
    
    public Guid FilmGuid { get; set; }
    
    public Guid CompanyGuid { get; set; }

}