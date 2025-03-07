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
    [PrimaryKey("FilmGuid", "OriginGuid")]
    public class FilmOrigin
    {
        [Ignore]
        [ForeignKey("FilmGuid")]
        public virtual Film Film { get; set; }

        [Ignore]
        [ForeignKey("OriginGuid")]
        public virtual Origin Origin { get; set; }

        //public int FilmId { get; set; }
        //public int OriginId { get; set; }
        
        public Guid FilmGuid { get; set; }
        
        public Guid OriginGuid { get; set; }

    }
}
