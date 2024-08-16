using System;
using System.Collections.Generic;
using System.Text;

namespace FrontEnd.Core.Data.Models
{
    public class Country
    {
        public int CountryId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public List<Film> Films { get; set; } = new List<Film>();
    }
}
