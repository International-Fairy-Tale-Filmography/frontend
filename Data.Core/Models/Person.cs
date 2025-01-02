using System.ComponentModel.DataAnnotations;

namespace Data.Core.Models;

public class Person
{
    [Key]
    public string PersonId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}