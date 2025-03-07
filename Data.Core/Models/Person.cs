using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;

namespace Data.Core.Models;

public class Person
{
    
    //public int PersonId { get; set; }
    [Key]
    public Guid Guid { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
}