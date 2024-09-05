using System;
using System.Collections.Generic;
using System.Text;



public class Company
{
        
    public int CompanyId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<Film> Films { get; set; } = new List<Film>();

}