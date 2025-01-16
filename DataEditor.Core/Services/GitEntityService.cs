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
            sb.AppendLine(await CommitChangesToGit<Country>());
            sb.AppendLine(await CommitChangesToGit<Film>());
            sb.AppendLine(await CommitChangesToGit<Origin>());
            sb.AppendLine(await CommitChangesToGit<Person>());
            sb.AppendLine(await CommitChangesToGit<Role>());

            sb.AppendLine(await CommitChangesToGit<FilmLink>());
            sb.AppendLine(await CommitChangesToGit<FilmCompany>());
            sb.AppendLine(await CommitChangesToGit<FilmCountry>());
            sb.AppendLine(await CommitChangesToGit<FilmLanguage>());
            sb.AppendLine(await CommitChangesToGit<FilmOrigin>());
            sb.AppendLine(await CommitChangesToGit<FilmPersonRole>());

            return sb.ToString();
        }
        public async Task<string> CommitChangesToGit<T>()
        {

            Dictionary<Type, Func<Task<string>>> handlers = new Dictionary<Type, Func<Task<string>>>
            {
                { typeof(Company), () => CommitChangesToGit( _context.Companies.OrderBy(i => i.CompanyId).ToList()) },
                { typeof(Country), () => CommitChangesToGit( _context.Countries.OrderBy(i => i.CountryId).ToList()) },
                { typeof(Film), () => CommitChangesToGit( _context.Films.OrderBy(i => i.FilmId).ToList()) },
                { typeof(Language), () => CommitChangesToGit( _context.Languages.OrderBy(i => i.LanguageId).ToList()) },
                { typeof(Origin), () => CommitChangesToGit( _context.Origins.OrderBy(i => i.OriginId).ToList()) },
                { typeof(Person), () => CommitChangesToGit( _context.People.OrderBy(i => i.PersonId).ToList()) },
                { typeof(Role), () => CommitChangesToGit( _context.Roles.OrderBy(i => i.RoleId).ToList()) },

                { typeof(FilmLink), () => CommitChangesToGit( _context.FilmLinks.OrderBy(i => i.LinkId).ThenBy(i => i.FilmId).ToList()) },
                { typeof(FilmCompany), () => CommitChangesToGit( _context.FilmCompanies.OrderBy(i => i.FilmId).ThenBy(i => i.CompanyId).ToList()) },
                { typeof(FilmCountry), () => CommitChangesToGit( _context.FilmCountries.OrderBy(i => i.FilmId).ThenBy(i => i.CountryId).ToList()) },
                { typeof(FilmLanguage), () => CommitChangesToGit( _context.FilmLanguages.OrderBy(i => i.FilmId).ThenBy(i => i.LanguageId).ToList()) },
                { typeof(FilmOrigin), () => CommitChangesToGit( _context.FilmOrigins.OrderBy(i => i.FilmId).ThenBy(i => i.OriginId).ToList()) },
                { typeof(FilmPersonRole), () => CommitChangesToGit( _context.FilmPersonRoles.OrderBy(i => i.Film).ThenBy(i => i.Person).ThenBy(i => i.RoleId).ToList()) } 
            };

            if (handlers.ContainsKey(typeof(T)))
            {
                return await handlers[typeof(T)]();
            }

            return "unknown";
        }

        public async Task<string> CommitChangesToGit<T>(List<T> objects)
        {
            if (!LoadedFiles.Contains(typeof(T)))
            {
                return "";
            }

            var filename = $"{dbSetNames[typeof(T)]}.csv";
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

        public static HashSet<Type> LoadedFiles = new ();


        public async Task SeedDataFromGit<T>()
        {
            if (!LoadedFiles.Contains(typeof(T)))
            {
                var entities = await FetchCsv<T>($"{dbSetNames[typeof(T)]}.csv");

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
                    await SeedDataFromGit<FilmLink>();
                    await SeedDataFromGit<FilmCountry>();
                    await SeedDataFromGit<FilmLanguage>();
                    await SeedDataFromGit<FilmOrigin>();
                    await SeedDataFromGit<FilmPersonRole>();
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

    }
}
