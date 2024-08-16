using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.Configuration.Attributes;

namespace FrontEnd.Core.Data.Models
{
    public class Film
    {
        public int FilmId { get; set; }
        public string Title { get; set; }
        [Ignore]
        public Company? Company { get; set; }
    }
}
