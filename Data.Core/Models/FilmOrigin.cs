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
    [PrimaryKey("FilmId", "OriginId")]
    public class FilmOrigin
    {
        [Ignore]
        [ForeignKey("FilmId")]
        public virtual Film Film { get; set; }

        [Ignore]
        [ForeignKey("OriginId")]
        public virtual Origin Origin { get; set; }

        public int FilmId { get; set; }
        public int OriginId { get; set; }
        
    }
}
