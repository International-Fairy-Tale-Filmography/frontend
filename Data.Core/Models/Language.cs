using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

public class Language
{
    [Key]
    public string LanguageId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}