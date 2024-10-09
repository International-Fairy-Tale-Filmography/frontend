﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using CsvHelper.Configuration.Attributes;

public class Film
{
    [Key]
    [Name("FilmId")]
    public int FilmId { get; set; }
    public string Title { get; set; }
    public string OtherTitle { get; set; }
    public string Color { get; set; }
    public int? ReleaseYear { get; set; }
    public int? Duration { get; set; }
    public string Mode { get; set; }
    public string Comment { get; set; }
    public bool? Published { get; set; }

    public List<Company> Companies { get; set; } = new List<Company>();

    public List<Country> Countries { get; set; } = new List<Country>();
}