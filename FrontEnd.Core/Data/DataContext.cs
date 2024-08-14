using FrontEnd.Core.Data.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrontEnd.Core.Data
{
    public class DataContext
    {
        public ICollection<Film> Films { get; set; }
    }
}
