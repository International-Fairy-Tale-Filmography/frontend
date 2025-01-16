using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Models
{
    [PrimaryKey("OriginId", "FilmId")]
    public class FilmOrigin
    {
        public int FilmId { get; set; }
        public int OriginId { get; set; }
        
    }
}
