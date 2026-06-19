using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;


namespace Data.Core.Models;

public class Company
{
    
    [Key]
    public Guid Guid { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public DateTime? CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }

}