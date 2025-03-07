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
    [PrimaryKey("FilmId", "LanguageId")]
    public class FilmLanguage
    {
        [Ignore]
        [ForeignKey("FilmId")]
        public virtual Film Film { get; set; }

        [Ignore]
        [ForeignKey("LanguageId")]
        public virtual Language Language { get; set; }

        public int FilmId { get; set; }
        public int LanguageId { get; set; }
        
        public Guid? FilmGuid { get; set; }
        
        public Guid? LanguageGuid { get; set; }


    }
}
