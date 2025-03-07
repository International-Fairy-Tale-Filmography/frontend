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
    [PrimaryKey("FilmId", "PersonId","RoleId")]
    public class FilmPersonRole
    {
        [Ignore]
        [ForeignKey("FilmId")]
        public virtual Film Film { get; set; }

        [Ignore]
        [ForeignKey("PersonId")]
        public virtual Person Person { get; set; }

        [Ignore]
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }


        public int FilmId { get; set; }
        public int PersonId { get; set; }
        public int RoleId { get; set; }

        
        public Guid? FilmGuid { get; set; }
        
        public Guid? PersonGuid { get; set; }
        
        public Guid? RoleGuid { get; set; }
    }
}
