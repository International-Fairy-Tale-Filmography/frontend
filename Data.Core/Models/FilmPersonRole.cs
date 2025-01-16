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
    [PrimaryKey("PersonId","FilmId","RoleId")]
    public class FilmPersonRole
    {
        public int FilmId { get; set; }
        public int PersonId { get; set; }
        public int RoleId { get; set; }

        //[ForeignKey("RoleId")]
        //public Role Role { get; set; }
    }
}
