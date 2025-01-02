using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Data.Core.Models;


public class Company
{
    [Key]
    public int CompanyId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<Film> Films { get; set; } = new List<Film>();

}