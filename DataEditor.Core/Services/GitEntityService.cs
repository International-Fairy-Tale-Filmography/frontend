using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Data.Core.Models;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Language = Data.Core.Models.Language;

namespace DataEditor.Core.Services
{
    public class GitEntityService
    {
        
        private readonly DataEditorDataContext _context;
        private readonly CoreSettingsModel _settings;
        private readonly GitService _gitService;

        public GitEntityService(DataEditorDataContext context, CoreSettingsModel settings, GitService gitService)
        {
            _context = context;
            _settings = settings;
            _gitService = gitService;
        }

        public async Task<string> CommitAllChangesToGit()
        {
            var sb = new StringBuilder();
            //todo make this loop the dictionary
            sb.AppendLine(await CommitChangesToGit<Company>());
            sb.AppendLine(await CommitChangesToGit<Country>());
            sb.AppendLine(await CommitChangesToGit<Film>());
            sb.AppendLine(await CommitChangesToGit<Origin>());
            sb.AppendLine(await CommitChangesToGit<Person>());
            sb.AppendLine(await CommitChangesToGit<Role>());

            sb.AppendLine(await CommitChangesToGit<FilmCompany>());
            //sb.AppendLine(await CommitChangesToGit<OriginFilm>());
            //sb.AppendLine(await CommitChangesToGit<CountryFilm>());
            //sb.AppendLine(await CommitChangesToGit<LanguageFilm>());

            return sb.ToString();
        }
        public async Task<string> CommitChangesToGit<T>()
        {

            Dictionary<Type, Func<Task<string>>> handlers = new Dictionary<Type, Func<Task<string>>>
            {
                { typeof(Company), () => CommitChangesToGit( _context.Companies.OrderBy(i => i.CompanyId).ToList(), "Company.csv") },
                { typeof(Country), () => CommitChangesToGit( _context.Countries.OrderBy(i => i.CountryId).ToList(), "Country.csv") },
                { typeof(Film), () => CommitChangesToGit( _context.Films.OrderBy(i => i.FilmId).ToList(), "Film.csv") },
                { typeof(Language), () => CommitChangesToGit( _context.Languages.OrderBy(i => i.LanguageId).ToList(), "Language.csv") },
                { typeof(Origin), () => CommitChangesToGit( _context.Origins.OrderBy(i => i.OriginId).ToList(), "Origin.csv") },
                { typeof(Person), () => CommitChangesToGit( _context.People.OrderBy(i => i.PersonId).ToList(), "Person.csv") },
                { typeof(Role), () => CommitChangesToGit( _context.Roles.OrderBy(i => i.RoleId).ToList(), "Role.csv") },

                { typeof(FilmCompany), () => CommitChangesToGit( _context.CompanyFilms.OrderBy(i => i.FilmId).ThenBy(i => i.CompanyId).ToList(), "CompanyFilm.csv") },
                //{ typeof(OriginFilm), () => CommitChangesToGit( _context.OriginFilms.OrderBy(i => i.FilmId).ThenBy(i => i.OriginId).ToList(), "OriginFilm.csv") },
                //{ typeof(CountryFilm), () => CommitChangesToGit( _context.CountryFilms.OrderBy(i => i.FilmId).ThenBy(i => i.CountryId).ToList(), "CountryFilm.csv") },
                //{ typeof(LanguageFilm), () => CommitChangesToGit( _context.LanguageFilms.OrderBy(i => i.FilmId).ThenBy(i => i.LanguageId).ToList(), "LanguageFilm.csv") },
                //{ typeof(PersonFilmRole), () => CommitChangesToGit( _context.Roles.ToList(), "PersonFilmRole.csv") } WIP
            };

            if (handlers.ContainsKey(typeof(T)))
            {
                return await handlers[typeof(T)]();
            }

            return "unknown";
        }

        public async Task<string> CommitChangesToGit<T>(List<T> objects, string filename)
        {
            if (!LoadedFiles.Contains(typeof(T)))
            {
                return "";
            }

            var file = await GetFileByName(filename);

            //var films = await context.Films.ToListAsync();

            await using var writer = new StringWriter();
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
            csv.WriteHeader<T>();
            await csv.NextRecordAsync();
            await csv.WriteRecordsAsync(objects);

            var content = writer.ToString();

            if (content != file.Content)
            {
                var result = await _gitService.UpdateFile(filename, file, content, $"test update at {DateTime.Now}");
                return filename + "; ";
            }
            else
            {
                return "";
            }


            
              
        }


        public async Task<RepositoryContent> GetFileByName(string name)
        {
            var filePath = Path.Combine(_settings.Folder, name);
            var file = await _gitService.GetFile(filePath);
            return file;
        }



        public static Dictionary<Type, string> fileName = new Dictionary<Type, string>()
        {
            {typeof(Film), "Film" },
            {typeof(Company), "Company"},
            {typeof(Country), "Country"},
            {typeof(Language), "Language"},
            {typeof(Origin), "Origin"},
            {typeof(Role), "Role"},
            {typeof(Person), "Person"},
            {typeof(FilmCompany), "CompanyFilm"},
        };

        public static Dictionary<Type, string> dbSetNames = new Dictionary<Type, string>()
        {
            {typeof(Company), "Companies"},
            {typeof(Country), "Countries"},
            {typeof(Film), "Films"},
            {typeof(Language), "Languages"},
            {typeof(Origin), "Origins"},
            {typeof(Person), "People"},
            {typeof(Role), "Roles"},
            {typeof(FilmCompany), "CompanyFilms"}
        };

        public static HashSet<Type> LoadedFiles = new ();


        public async Task SeedDataFromGit<T>()
        {
            if (!LoadedFiles.Contains(typeof(T)))
            {
                var entities = await FetchCsv<T>($"{fileName[typeof(T)]}.csv");

                //get the property method for the appropriate entity
                var dbSetName = dbSetNames[typeof(T)];
                var dbSetProperty = _context.GetType().GetProperty(dbSetName);

                var dbSet = dbSetProperty.GetValue(_context);

                //call the dbset's addrange method
                var addRangeMethod = dbSet.GetType().GetMethod("AddRange", new[] { typeof(IEnumerable<T>) });
                addRangeMethod.Invoke(dbSet, new object[] { entities });

                //mark the file as loaded
                LoadedFiles.Add(typeof(T));

                //if the file is FILM, then load everything and map everything
                if (typeof(T) == typeof(Film))
                {
                    await SeedDataFromGit<Company>();
                    await SeedDataFromGit<Country>();
                    await SeedDataFromGit<Language>();
                    await SeedDataFromGit<Origin>();
                    await SeedDataFromGit<Role>();
                    await SeedDataFromGit<Person>();

                    await SeedDataFromGit<FilmCompany>();

                   // await MapCompanyFilms();
                    await MapCountryFilms();
                    await MapLanguageFilms();
                    await MapOriginFilms();
                }
            }
            
            await _context.SaveChangesAsync();
        }


        private async Task<List<T>> FetchCsv<T>(string name)
        {
            var file = await GetFileByName(name);

            using var reader = new StringReader(file.Content);

            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            var records = csv.GetRecords<T>();

            return records.ToList();
        }

        //private async Task MapCompanyFilms()
        //{
        //    var companyFilms = await FetchCsv<CompanyFilm>("CompanyFilm.csv");

        //    var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
        //    var companyDict = _context.Companies.ToDictionary(i => i.CompanyId, i => i);

        //    foreach (var i in companyFilms)
        //    {
              
        //         var film = filmDict[i.FilmId];

        //        var company = companyDict[i.CompanyId];

        //        film.Companies.Add(company);
        //        //company.Films.Add(film);
        //    }

        //    LoadedFiles.Add(typeof(CompanyFilm));

        //}


        private async Task MapCountryFilms()
        {
            var companyFilms = await FetchCsv<FilmCountry>("CountryFilm.csv");

            var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
            var countryDict = _context.Countries.ToDictionary(i => i.CountryId, i => i);

            foreach (var i in companyFilms)
            {
                var film = filmDict[i.FilmId];

                var country = countryDict[i.CountryId];

                film.Countries.Add(country);
                //country.Films.Add(film);
            }

            LoadedFiles.Add(typeof(FilmCountry));

        }

        private async Task MapLanguageFilms()
        {
            var languageFilms = await FetchCsv<FilmLanguage>("LanguageFilm.csv");

            var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
            var languageDict = _context.Languages.ToDictionary(i => i.LanguageId, i => i);

            foreach (var i in languageFilms)
            {
                if (!filmDict.ContainsKey(i.FilmId))
                {
                    continue;
                }

                if (!languageDict.ContainsKey(i.LanguageId))
                {
                    continue;
                }

                var film = filmDict[i.FilmId];

                var language = languageDict[i.LanguageId];

                film.Languages.Add(language);
              
            }

            LoadedFiles.Add(typeof(FilmLanguage));
        }

        private async Task MapOriginFilms()
        {
            var OriginFilms = await FetchCsv<FilmOrigin>("OriginFilm.csv");

            var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
            var originDict = _context.Origins.ToDictionary(i => i.OriginId, i => i);

            foreach (var i in OriginFilms)
            {
                if (!filmDict.ContainsKey(i.FilmId))
                {
                    continue;
                }

                if (!originDict.ContainsKey(i.OriginId))
                {
                    continue;
                }

                var film = filmDict[i.FilmId];

                var Origin = originDict[i.OriginId];

                film.Origins.Add(Origin);

            }

            LoadedFiles.Add(typeof(FilmOrigin));
        }

    }
}
