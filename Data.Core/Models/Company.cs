using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;


namespace Data.Core.Models;

public class Company
{
    
    [Key]
    public Guid Guid { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

}