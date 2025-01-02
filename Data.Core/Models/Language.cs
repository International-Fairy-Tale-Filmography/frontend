using System.ComponentModel.DataAnnotations;

namespace Data.Core.Models;

public class Language
{
    [Key]
    public string LanguageId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}