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


        public async Task<string> CommitChangesToGit<T>()
        {

            var handlers = new Dictionary<Type, Func<Task<string>>>
            {
                //{ typeof(Film), () => CommitChangesToGit( context.People.ToList(), "Person.csv") },
                //{ typeof(Film), () => CommitChangesToGit( context.Films.ToList(), "Film.csv") },
                //{ typeof(Origin), () => CommitChangesToGit( context.Origins.ToList(), "Origin.csv") }
            };

            if (handlers.ContainsKey(typeof(T)))
            {
                return await handlers[typeof(T)]();
            }

            return "unknown";
        }

        public async Task<string> CommitChangesToGit<T>(List<T> objects, string filename)
        {
            //var filename = "Film.csv";
            var filePath = Path.Combine(_settings.Folder, filename);
            var file = await GetFileByName(filename);

            //var films = await context.Films.ToListAsync();

            await using var writer = new StringWriter();
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
            csv.WriteHeader<T>();
            await csv.NextRecordAsync();
            await csv.WriteRecordsAsync(objects);

            var content = writer.ToString();

            var result = await _gitService.UpdateFile(filename, file, content, $"test update at {DateTime.Now}");

             return result.Commit.Sha;
             // Snackbar.Add("Saved commit " + result.Commit.Sha, Severity.Success);
        }


        public async Task<RepositoryContent> GetFileByName(string name)
        {
            var filePath = Path.Combine(_settings.Folder, name);
            var file = await _gitService.GetFile(filePath);
            return file;
        }


        public async Task SeedDataFromGit()
        {
            //_context.Companies.AddRange(await FetchCsv<Company>("Company.csv"));



            //done
            //_context.Countries.AddRange(await FetchCsv<Country>("Country.csv"));
            //_context.Languages.AddRange(await FetchCsv<Language>("Language.csv"));
            _context.Films.AddRange(await FetchCsv<Film>("Film.csv"));
            _context.Origins.AddRange(await FetchCsv<Origin>("Origin.csv"));
            //context.People.AddRange(await FetchCsv<Person>("Person.csv"));

            await _context.SaveChangesAsync();

            //await MapCompanyFilms();
            //await MapCountryFilms();
            //await MapLanguageFilms();
            await MapOriginFilms();

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

        private async Task MapCompanyFilms()
        {
            var companyFilms = await FetchCsv<CompanyFilm>("CompanyFilm.csv");

            var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
            var companyDict = _context.Companies.ToDictionary(i => i.CompanyId, i => i);

            foreach (var i in companyFilms)
            {
              
                 var film = filmDict[i.FilmId];

                var company = companyDict[i.CompanyId];

                film.Companies.Add(company);
                //company.Films.Add(film);
            }

        }


        private async Task MapCountryFilms()
        {
            var companyFilms = await FetchCsv<CountryFilm>("CountryFilm.csv");

            var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
            var countryDict = _context.Countries.ToDictionary(i => i.CountryId, i => i);

            foreach (var i in companyFilms)
            {
                var film = filmDict[i.FilmId];

                var country = countryDict[i.CountryId];

                film.Countries.Add(country);
                //country.Films.Add(film);
            }

        }

        private async Task MapLanguageFilms()
        {
            var languageFilms = await FetchCsv<LanguageFilm>("LanguageFilm.csv");

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

        }

        private async Task MapOriginFilms()
        {
            var OriginFilms = await FetchCsv<OriginFilm>("OriginFilm.csv");

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

        }

    }
}
