﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Models
{
    [PrimaryKey("LanguageId", "FilmId")]
    public class LanguageFilm
    {
        public int LanguageId { get; set; }
        public int FilmId { get; set; }
    }
}
