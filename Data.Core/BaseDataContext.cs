using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Data.Core
{
    public abstract class BaseDataContext : DbContext
    {
        public List<Company> Companies { get; set; }
        public List<Country> Countries { get; set; }
        public List<Film> Films { get; set; }
        public List<Language> Languages { get; set; }
        public List<Origin> Origins { get; set; }
        public List<Person> People { get; set; }
    }
}
