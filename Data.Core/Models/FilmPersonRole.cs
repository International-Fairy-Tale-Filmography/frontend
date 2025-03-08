using CsvHelper.Configuration.Attributes;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Models
{
    [PrimaryKey("FilmGuid", "PersonGuid","RoleGuid")]
    public class FilmPersonRole
    {
        [Ignore]
        [ForeignKey("FilmGuid")]
        public virtual Film Film { get; set; }

        [Ignore]
        [ForeignKey("PersonGuid")]
        public virtual Person Person { get; set; }

        [Ignore]
        [ForeignKey("RoleGuid")]
        public virtual Role Role { get; set; }
        
        public Guid FilmGuid { get; set; }
        
        public Guid PersonGuid { get; set; }
        
        public Guid RoleGuid { get; set; }
    }
}
