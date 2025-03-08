using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;

namespace Data.Core.Models;

public class Country
{

    [Key]
    public Guid Guid { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; }
}