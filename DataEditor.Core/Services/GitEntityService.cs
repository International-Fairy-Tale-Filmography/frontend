using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Data.Core.Configuration;
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
            sb.AppendLine(await CommitChangesToGit<Language>());
            sb.AppendLine(await CommitChangesToGit<Company>());


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
                { typeof(Company), () => CommitChangesToGit( _context.Companies.OrderBy(i => i.Guid).ToList()) },
                { typeof(Country), () => CommitChangesToGit( _context.Countries.OrderBy(i => i.Guid).ToList()) },
                { typeof(Film), () => CommitChangesToGit( _context.Films.OrderBy(i => i.Guid).ToList()) },
                { typeof(Language), () => CommitChangesToGit( _context.Languages.OrderBy(i => i.Guid).ToList()) },
                { typeof(Origin), () => CommitChangesToGit( _context.Origins.OrderBy(i => i.Guid).ToList()) },
                { typeof(Person), () => CommitChangesToGit( _context.People.OrderBy(i => i.Guid).ToList()) },
                { typeof(Role), () => CommitChangesToGit( _context.Roles.OrderBy(i => i.Guid).ToList()) },

                { typeof(FilmLink), () => CommitChangesToGit( _context.FilmLinks.OrderBy(i => i.LinkId).ThenBy(i => i.FilmGuid).ToList()) },
                { typeof(FilmCompany), () => CommitChangesToGit( _context.FilmCompanies.OrderBy(i => i.FilmGuid).ThenBy(i => i.CompanyGuid).ToList()) },
                { typeof(FilmCountry), () => CommitChangesToGit( _context.FilmCountries.OrderBy(i => i.FilmGuid).ThenBy(i => i.CountryGuid).ToList()) },
                { typeof(FilmLanguage), () => CommitChangesToGit( _context.FilmLanguages.OrderBy(i => i.FilmGuid).ThenBy(i => i.LanguageGuid).ToList()) },
                { typeof(FilmOrigin), () => CommitChangesToGit( _context.FilmOrigins.OrderBy(i => i.FilmGuid).ThenBy(i => i.OriginGuid).ToList()) },
                { typeof(FilmPersonRole), () => CommitChangesToGit( _context.FilmPersonRoles.OrderBy(i => i.Film).ThenBy(i => i.Person).ThenBy(i => i.RoleGuid).ToList()) } 
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

            var filename = $"{CoreSettings.dbSetNames[typeof(T)]}.csv";
            var file = await GetFileByName(filename);

            await using var writer = new StringWriter();
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
            csv.WriteHeader<T>();
            await csv.NextRecordAsync();
            await csv.WriteRecordsAsync(objects);

            var content = writer.ToString();

            if (content != file.Content)
            {
                //var result = await _gitService.UpdateFile(filename, file, content, $"test update at {DateTime.Now}");
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




        public static HashSet<Type> LoadedFiles = new ();


        public async Task SeedDataFromGit<T>()
        {
            if (!LoadedFiles.Contains(typeof(T)))
            {
                var fileName = $"{CoreSettings.dbSetNames[typeof(T)]}.csv";
                var entities = await FetchCsv<T>(fileName);

                //get the property method for the appropriate entity
                var dbSetName = CoreSettings.dbSetNames[typeof(T)];
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
                    //await FillInGuids();
                }
            }


            await _context.SaveChangesAsync();
           
        }

        private async Task FillInGuids()
        {
            await _context.SaveChangesAsync();
      
            foreach (var item in _context.Films.ToList())
            {
                if (item.Guid == null)
                {
                    item.Guid = Guid.NewGuid();
                }
            }

            foreach (var item in _context.People.ToList())
            {
                if (item.Guid == null)
                {
                    item.Guid = Guid.NewGuid();
                }
            }

            foreach (var item in _context.Roles.ToList())
            {
                if (item.Guid == null)
                {
                    item.Guid = Guid.NewGuid();
                }
            }

            foreach (var item in _context.Origins.ToList())
            {
                if (item.Guid == null)
                {
                    item.Guid = Guid.NewGuid();
                }
            }

            foreach (var item in _context.Languages.ToList())
            {
                if (item.Guid == null)
                {
                    item.Guid = Guid.NewGuid();
                }
            }

            foreach (var item in _context.Countries.ToList())
            {
                if (item.Guid == null)
                {
                    item.Guid = Guid.NewGuid();
                }
            }

            foreach (var item in _context.Companies.ToList())
            {
                if (item.Guid == null)
                {
                    item.Guid = Guid.NewGuid();
                }
            }

            foreach (var item in _context.FilmCompanies.ToList())
            {
                if (item.FilmGuid == null)
                {
                    item.FilmGuid = item.Film.Guid;
                }

                if (item.CompanyGuid == null)
                {
                    item.CompanyGuid = item.Company.Guid;
                }
            }

            foreach (var item in _context.FilmLinks.ToList())
            {
                if (item.FilmGuid == null)
                {
                    item.FilmGuid = item.Film.Guid;
                }

                if (item.Guid == null)
                {
                    item.Guid = Guid.NewGuid();
                }
            }

            foreach (var item in _context.FilmCountries.ToList())
            {
                if (item.FilmGuid == null)
                {
                    item.FilmGuid = item.Film.Guid;
                }

                if (item.CountryGuid == null)
                {
                    item.CountryGuid = item.Country.Guid;
                }
            }


            foreach (var item in _context.FilmLanguages.ToList())
            {
                if (item.FilmGuid == null)
                {
                    item.FilmGuid = item.Film.Guid;
                }

                if (item.LanguageGuid == null && item.Language != null)
                {
                    item.LanguageGuid = item.Language.Guid;
                }
            }

            foreach (var item in _context.FilmOrigins.ToList())
            {
                if (item.FilmGuid == null)
                {
                    item.FilmGuid = item.Film.Guid;
                }

                if (item.OriginGuid == null)
                {
                    item.OriginGuid = item.Origin.Guid;
                }
            }

            foreach (var item in _context.FilmPersonRoles.ToList())
            {
                if (item.FilmGuid == null)
                {
                    item.FilmGuid = item.Film.Guid;
                }

                if (item.PersonGuid == null)
                {
                    item.PersonGuid = item.Person.Guid;
                }

                if (item.RoleGuid == null)
                {
                    item.RoleGuid = item.Role.Guid;
                }
            }




            await _context.SaveChangesAsync();
        }

        private async Task<List<T>> FetchCsv<T>(string name)
        {
            var repositoryFile = await GetFileByName(name);

            var content = await GetFileContentFromDownloadUrl(repositoryFile);
            using var reader = new StringReader(content);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var csv = new CsvReader(reader, config);
            
            var records = csv.GetRecords<T>();

            return records.ToList();
        }

        public async Task<string> GetFileContentFromDownloadUrl(RepositoryContent repositoryContent)
        {
            if (repositoryContent == null)
                throw new ArgumentNullException(nameof(repositoryContent));
                
            if (string.IsNullOrEmpty(repositoryContent.DownloadUrl))
                throw new InvalidOperationException("DownloadUrl is null or empty for the repository content");
            
            using var httpClient = new HttpClient();
            try
            {
                // Download the content directly from the DownloadUrl
                var response = await httpClient.GetAsync(repositoryContent.DownloadUrl);
                response.EnsureSuccessStatusCode();
                
                // Return the content as a string
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error downloading file content from URL: {repositoryContent.DownloadUrl}", ex);
            }
        }
    }
}
