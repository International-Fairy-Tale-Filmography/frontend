using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;

namespace Data.Core.Models
{
    [PrimaryKey("FilmGuid", "LanguageGuid")]
    public class FilmLanguage
    {
        [Ignore]
        [ForeignKey("FilmGuid")]
        public virtual Film Film { get; set; }

        [Ignore]
        [ForeignKey("LanguageGuid")]
        public virtual Language Language { get; set; }
        
        public Guid FilmGuid { get; set; }
        
        public Guid LanguageGuid { get; set; }


    }
}
