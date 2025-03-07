using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Models
{
    public class FilmLink
    {
        [Key] 
        public int LinkId { get; set; }

        [Ignore]
        [ForeignKey("FilmGuid")]
        public virtual Film Film { get; set; }
       // public int FilmId { get; set; }
        
        public Guid Guid { get; set; }

        
        public Guid FilmGuid { get; set; }
        public string Url { get; set; }
    }
}
