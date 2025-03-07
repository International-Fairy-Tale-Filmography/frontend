using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;

namespace Data.Core.Models;

public class Origin
{
    
    //public int OriginId { get; set; }
    [Key]
    public Guid Guid { get; set; }
    public string Title { get; set; }
    public string Code { get; set; }
    public string Author { get; set; }
    


}