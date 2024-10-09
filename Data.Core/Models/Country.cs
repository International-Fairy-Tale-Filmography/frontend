using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

public class Country
{
    [Key]
    public int CountryId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; }
    public List<Film> Films { get; set; } = new List<Film>();
}