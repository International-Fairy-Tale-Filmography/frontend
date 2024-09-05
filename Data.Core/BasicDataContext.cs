using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    public class BasicDataContext
    {
        public List<Company> Companies { get; set; } = new List<Company>();
        public List<Country> Countries { get; set; } = new List<Country>();
        public List<Film> Films { get; set; } = new List<Film>();
        public List<Language> Languages { get; set; } = new List<Language>();
        public List<Origin> Origins { get; set; } = new List<Origin>();
        public List<Person> People { get; set; } = new List<Person>();
    }

