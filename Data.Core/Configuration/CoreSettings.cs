using Data.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Data.Core.Configuration
{
    public class CoreSettings
    {
        public static Dictionary<Type, string> dbSetNames = new Dictionary<Type, string>()
        {
            {typeof(Company), "Companies"},
            {typeof(Country), "Countries"},
            {typeof(Film), "Films"},
            {typeof(Language), "Languages"},
            {typeof(Origin), "Origins"},
            {typeof(Person), "People"},
            {typeof(Role), "Roles"},
            {typeof(FilmLink), "FilmLinks"},
            {typeof(FilmCompany), "FilmCompanies"},
            {typeof(FilmCountry), "FilmCountries"},
            {typeof(FilmLanguage), "FilmLanguages"},
            {typeof(FilmOrigin), "FilmOrigins"},
            {typeof(FilmPersonRole), "FilmPersonRoles"}
        };

        public static Guid DirectorRoleId = new("33a0105e-7920-4e0e-9298-68be8fcb56fd");
    }
}
