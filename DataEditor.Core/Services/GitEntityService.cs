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

                { typeof(FilmLink), () => CommitChangesToGit( _context.FilmLinks.OrderBy(i => i.Guid).ThenBy(i => i.FilmGuid).ToList()) },
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

            //get the content on the server
            var oldContent = await GetFileContentFromDownloadUrl(file);

            var newContent = writer.ToString();
            //check if theres any differences
            if (oldContent != newContent)
            {
                //update the server with latest if theres any differences
                var result = await _gitService.UpdateFile(filename, file, newContent, $"updating {filename}");
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


        // Modify the SeedDataFromGit method to accept a progress callback
        public async Task SeedDataFromGit<T>(Action<string> progressCallback = null)
        {
            if (!LoadedFiles.Contains(typeof(T)))
            {
                var fileName = $"{CoreSettings.dbSetNames[typeof(T)]}.csv";
                progressCallback?.Invoke($"Loading {fileName}...");
                
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
                
                // Remove the automatic loading of related tables for Film to avoid recursive calls
                // We'll handle this separately through SeedRelatedDataInBackground
            }

            await _context.SaveChangesAsync();
            progressCallback?.Invoke("Data loading complete");
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

        // Add a new method to load only Film data initially
        public async Task SeedFilmDataOnly(Action<string> progressCallback = null)
        {
            progressCallback?.Invoke("Loading Films...");
            
            if (!LoadedFiles.Contains(typeof(Film)))
            {
                var fileName = $"{CoreSettings.dbSetNames[typeof(Film)]}.csv";
                progressCallback?.Invoke($"Loading {fileName}...");
                
                var entities = await FetchCsv<Film>(fileName);

                var dbSetProperty = _context.GetType().GetProperty("Films");
                var dbSet = dbSetProperty.GetValue(_context);
                
                var addRangeMethod = dbSet.GetType().GetMethod("AddRange", new[] { typeof(IEnumerable<Film>) });
                addRangeMethod.Invoke(dbSet, new object[] { entities });
                
                LoadedFiles.Add(typeof(Film));
                await _context.SaveChangesAsync();
            }
            
            progressCallback?.Invoke("Films loaded successfully");
        }

        // Add a method to load related data in the background
        private static readonly List<Type> _relatedEntityTypes = new List<Type>
        {
            typeof(Company),
            typeof(Country),
            typeof(Language),
            typeof(Origin),
            typeof(Role),
            typeof(Person),
            typeof(FilmCompany),
            typeof(FilmLink),
            typeof(FilmCountry),
            typeof(FilmLanguage),
            typeof(FilmOrigin),
            typeof(FilmPersonRole)
        };

        // Modify the method to load related data in the background with progress tracking
        public async Task SeedRelatedDataInBackground(Action<string, int, int> progressCallback = null)
        {
            // Calculate total files to load
            int totalFiles = _relatedEntityTypes.Count;
            int currentFile = 0;
            
            progressCallback?.Invoke("Starting background data load...", currentFile, totalFiles);
            
            foreach (var entityType in _relatedEntityTypes)
            {
                // Skip if already loaded
                if (LoadedFiles.Contains(entityType))
                {
                    currentFile++;
                    progressCallback?.Invoke($"Skipping {CoreSettings.dbSetNames[entityType]}.csv (already loaded)", currentFile, totalFiles);
                    continue;
                }
                
                var fileName = $"{CoreSettings.dbSetNames[entityType]}.csv";
                currentFile++;
                progressCallback?.Invoke($"Loading {fileName}...", currentFile, totalFiles);
                
                try
                {
                    // Use reflection to call the generic SeedDataFromGit method
                    var method = typeof(GitEntityService).GetMethod(nameof(SeedDataFromGit));
                    var genericMethod = method.MakeGenericMethod(entityType);
                    
                    // Create a progress callback wrapper that preserves the counting context
                    Action<string> entityProgressCallback = (message) => 
                        progressCallback?.Invoke(message, currentFile, totalFiles);
                        
                    await (Task)genericMethod.Invoke(this, new object[] { entityProgressCallback });
                }
                catch (Exception ex)
                {
                    progressCallback?.Invoke($"Error loading {fileName}: {ex.Message}", currentFile, totalFiles);
                }
            }
            
            progressCallback?.Invoke("All related data loaded successfully", totalFiles, totalFiles);
        }

        // An overload of SeedRelatedDataInBackground that works with the old signature for backward compatibility
        public async Task SeedRelatedDataInBackground(Action<string> progressCallback = null)
        {
            await SeedRelatedDataInBackground((message, current, total) => 
                progressCallback?.Invoke($"{message} ({current}/{total})"));
        }
    }
}
