using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;

namespace Data.Core.Models;

public class Country
{
    [Key]
    public int CountryId { get; set; }
    public Guid? Guid { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; }
    public List<Film> Films { get; set; } = new List<Film>();
}