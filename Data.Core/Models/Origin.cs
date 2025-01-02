using System.ComponentModel.DataAnnotations;

namespace Data.Core.Models;

public class Origin
{
    [Key]
    public int OriginId { get; set; }
    public string Title { get; set; }
    public string Code { get; set; }
    public string Author { get; set; }
    
        
}