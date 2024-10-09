using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
public class Origin
{
    [Key]
    public int OriginId { get; set; }
    public string Code { get; set; }
    public string Author { get; set; }
        
}