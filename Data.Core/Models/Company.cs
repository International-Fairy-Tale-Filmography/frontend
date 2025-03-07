using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;


namespace Data.Core.Models;

public class Company
{
    [Key]
    public int CompanyId { get; set; }
    public Guid? Guid { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<Film> Films { get; set; } = new List<Film>();

}