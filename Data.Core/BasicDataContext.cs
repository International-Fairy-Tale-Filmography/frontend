using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core;


public class BasicDataContext : BaseDataContext
{
    public BasicDataContext()
    {
        Companies = new List<Company>();
        Countries = new List<Country>();
        Films = new List<Film>();
        Languages = new List<Language>();
        Origins = new List<Origin>();
        People = new List<Person>();
    }
}

