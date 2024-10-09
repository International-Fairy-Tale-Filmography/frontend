using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

public class Person
{
    [Key]
    public string PersonId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}