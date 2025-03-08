using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;

namespace Data.Core.Models
{
    public class Role
    {
        
        [Key]
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
    }
}
