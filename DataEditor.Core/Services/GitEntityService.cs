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

        public async Task<string> CommitAllChangesToGit(Action<string> progressCallback = null)
        {
            var sb = new StringBuilder();

            var entitiesToCommit = new List<Func<Task<string>>>
            {
                () => CommitChangesToGit<Country>(),
                () => CommitChangesToGit<Film>(),
                () => CommitChangesToGit<Origin>(),
                () => CommitChangesToGit<Person>(),
                () => CommitChangesToGit<Role>(),
                () => CommitChangesToGit<Language>(),
                () => CommitChangesToGit<Company>(),
                () => CommitChangesToGit<FilmLink>(),
                () => CommitChangesToGit<FilmCompany>(),
                () => CommitChangesToGit<FilmCountry>(),
                () => CommitChangesToGit<FilmLanguage>(),
                () => CommitChangesToGit<FilmOrigin>(),
                () => CommitChangesToGit<FilmPersonRole>()
            };

            int totalFiles = entitiesToCommit.Count;
            int currentFile = 0;

            foreach (var commitAction in entitiesToCommit)
            {
                currentFile++;
                var result = await commitAction();
                sb.AppendLine(result);

                // Report progress
                progressCallback?.Invoke($"Committed {currentFile}/{totalFiles} files.");
            }

            // After successful commit, reset caches to reflect the new state
            progressCallback?.Invoke("Resetting caches to reflect latest changes...");
            await ResetCaches();
            progressCallback?.Invoke("Cache reset completed.");

            return sb.ToString();
        }

        public async Task<List<FileDifference>> GetAllDifferences()
        {
            var differences = new List<FileDifference>();

            // Same entities from CommitAllChangesToGit
            differences.Add(await GetDifferences<Country>());
            differences.Add(await GetDifferences<Film>());
            differences.Add(await GetDifferences<Origin>());
            differences.Add(await GetDifferences<Person>());
            differences.Add(await GetDifferences<Role>());
            differences.Add(await GetDifferences<Language>());
            differences.Add(await GetDifferences<Company>());

            differences.Add(await GetDifferences<FilmLink>());
            differences.Add(await GetDifferences<FilmCompany>());
            differences.Add(await GetDifferences<FilmCountry>());
            differences.Add(await GetDifferences<FilmLanguage>());
            differences.Add(await GetDifferences<FilmOrigin>());
            differences.Add(await GetDifferences<FilmPersonRole>());

            return differences;
        }

        public async Task<FileDifference> GetDifferences<T>()
        {
            Dictionary<Type, Func<Task<FileDifference>>> handlers = new Dictionary<Type, Func<Task<FileDifference>>>
            {
                { typeof(Company), () => GetDifferences(_context.Companies.OrderBy(i => i.Guid).ToList()) },
                { typeof(Country), () => GetDifferences(_context.Countries.OrderBy(i => i.Guid).ToList()) },
                { typeof(Film), () => GetDifferences(_context.Films.OrderBy(i => i.Guid).ToList()) },
                { typeof(Language), () => GetDifferences(_context.Languages.OrderBy(i => i.Guid).ToList()) },
                { typeof(Origin), () => GetDifferences(_context.Origins.OrderBy(i => i.Guid).ToList()) },
                { typeof(Person), () => GetDifferences(_context.People.OrderBy(i => i.Guid).ToList()) },
                { typeof(Role), () => GetDifferences(_context.Roles.OrderBy(i => i.Guid).ToList()) },

                { typeof(FilmLink), () => GetDifferences(_context.FilmLinks.OrderBy(i => i.Guid).ThenBy(i => i.FilmGuid).ToList()) },
                { typeof(FilmCompany), () => GetDifferences(_context.FilmCompanies.OrderBy(i => i.FilmGuid).ThenBy(i => i.CompanyGuid).ToList()) },
                { typeof(FilmCountry), () => GetDifferences(_context.FilmCountries.OrderBy(i => i.FilmGuid).ThenBy(i => i.CountryGuid).ToList()) },
                { typeof(FilmLanguage), () => GetDifferences(_context.FilmLanguages.OrderBy(i => i.FilmGuid).ThenBy(i => i.LanguageGuid).ToList()) },
                { typeof(FilmOrigin), () => GetDifferences(_context.FilmOrigins.OrderBy(i => i.FilmGuid).ThenBy(i => i.OriginGuid).ToList()) },
                { typeof(FilmPersonRole), () => GetDifferences(_context.FilmPersonRoles.OrderBy(i => i.Film).ThenBy(i => i.Person).ThenBy(i => i.RoleGuid).ToList()) } 
            };

            if (handlers.ContainsKey(typeof(T)))
            {
                return await handlers[typeof(T)]();
            }

            return new FileDifference 
            { 
                FileName = "unknown",
                OldContent = string.Empty,
                NewContent = string.Empty,
                ChangedLinesCount = 0
            };
        }

        public async Task<FileDifference> GetDifferences<T>(List<T> objects)
        {
            var filename = $"{CoreSettings.dbSetNames[typeof(T)]}.csv";
            var fileDiff = new FileDifference
            {
                FileName = filename,
                OldContent = string.Empty,
                NewContent = string.Empty,
                ChangedLinesCount = 0
            };

            if (!LoadedFiles.Contains(typeof(T)))
            {
                return fileDiff;
            }

            try
            {
                var file = await GetFileByName(filename);

                await using var writer = new StringWriter();
                await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
                csv.WriteHeader<T>();
                await csv.NextRecordAsync();
                await csv.WriteRecordsAsync(objects);

                // Get the content on the server
                var oldContent = await GetFileContentFromDownloadUrl(file);
                var newContent = writer.ToString();

                fileDiff.OldContent = oldContent;
                fileDiff.NewContent = newContent;

                // Simple way to calculate differences (line by line)
                var oldLines = oldContent.Split('\n');
                var newLines = newContent.Split('\n');

                // Count changed lines
                int changedLines = 0;
                for (int i = 0; i < Math.Max(oldLines.Length, newLines.Length); i++)
                {
                    var oldLine = i < oldLines.Length ? oldLines[i] : null;
                    var newLine = i < newLines.Length ? newLines[i] : null;

                    if (oldLine != newLine)
                    {
                        changedLines++;
                    }
                }

                fileDiff.ChangedLinesCount = changedLines;
            }
            catch (Exception ex)
            {
                // Handle exceptions - log, return empty differences, etc.
                System.Diagnostics.Debug.WriteLine($"Error getting differences for {filename}: {ex.Message}");
            }

            return fileDiff;
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

        private async Task UpdateSyncedCommitSnapshot()
        {
            var latestCommit = await _gitService.GetLatestBranchCommitSha();
            await _gitService.SetSyncedCommitSha(latestCommit);
        }

        private async Task<bool> HasLocalData<T>() where T : class
        {
            return await _context.Set<T>().AsNoTracking().AnyAsync();
        }

        private async Task BulkInsertAsync<T>(IReadOnlyList<T> entities, int batchSize = 2000) where T : class
        {
            if (entities.Count == 0)
            {
                return;
            }

            var previousAutoDetect = _context.ChangeTracker.AutoDetectChangesEnabled;

            try
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                for (int i = 0; i < entities.Count; i += batchSize)
                {
                    var batch = entities.Skip(i).Take(batchSize);
                    await _context.Set<T>().AddRangeAsync(batch);
                    await _context.SaveChangesAsync();
                    _context.ChangeTracker.Clear();
                }
            }
            finally
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = previousAutoDetect;
            }
        }

        public async Task SeedDataFromGit<T>(Action<string> progressCallback = null) where T : class
        {
            if (LoadedFiles.Contains(typeof(T)))
            {
                progressCallback?.Invoke("Data loading complete");
                return;
            }

            if (await HasLocalData<T>())
            {
                LoadedFiles.Add(typeof(T));
                progressCallback?.Invoke("Data loading complete");
                return;
            }

            var fileName = $"{CoreSettings.dbSetNames[typeof(T)]}.csv";
            progressCallback?.Invoke($"Loading {fileName}...");

            var entities = await FetchCsv<T>(fileName);
            await BulkInsertAsync(entities);

            LoadedFiles.Add(typeof(T));
            await UpdateSyncedCommitSnapshot();
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

            // Get file content with cache-busting parameter
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
                // Extract the download URL and append the commit SHA for cache-busting
                string downloadUrl = repositoryContent.DownloadUrl;
                
                downloadUrl += $"?ref={repositoryContent.Sha}";

                // Download the content
                var response = await httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                
                // Return the content as a string
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error downloading file content from URL: {repositoryContent.DownloadUrl}", ex);
            }
        }

        public async Task SeedFilmDataOnly(Action<string> progressCallback = null)
        {
            progressCallback?.Invoke("Loading Films...");

            if (LoadedFiles.Contains(typeof(Film)))
            {
                progressCallback?.Invoke("Films loaded successfully");
                return;
            }

            if (await HasLocalData<Film>())
            {
                LoadedFiles.Add(typeof(Film));
                progressCallback?.Invoke("Films loaded successfully");
                return;
            }

            var fileName = $"{CoreSettings.dbSetNames[typeof(Film)]}.csv";
            progressCallback?.Invoke($"Loading {fileName}...");

            var entities = await FetchCsv<Film>(fileName);
            await BulkInsertAsync(entities);

            LoadedFiles.Add(typeof(Film));
            await UpdateSyncedCommitSnapshot();
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

        public async Task<bool> HasLocalSnapshot()
        {
            return await _context.Films.AsNoTracking().AnyAsync();
        }

        public async Task<bool> IsLocalSnapshotOutdated()
        {
            if (!await HasLocalSnapshot())
            {
                return false;
            }

            var syncedCommit = await _gitService.GetSyncedCommitSha();
            if (string.IsNullOrWhiteSpace(syncedCommit))
            {
                return false;
            }

            var latestCommit = await _gitService.GetLatestBranchCommitSha();
            return !string.Equals(syncedCommit, latestCommit, StringComparison.OrdinalIgnoreCase);
        }

        public async Task RefreshFromGitHub(Action<string> progressCallback = null)
        {
            await ResetCaches();
            await SeedFilmDataOnly(progressCallback);
            await SeedRelatedDataInBackground(progressCallback);
        }

        public class FileDifference
        {
            public string FileName { get; set; }
            public string OldContent { get; set; }
            public string NewContent { get; set; }
            public int ChangedLinesCount { get; set; }
        }

        public async Task ResetCaches()
        {
            _context.ClearAllDbSets();
            _context.DetachAllEntities();
            LoadedFiles.Clear();
            await _gitService.SetSyncedCommitSha(null);
        }
    }
}
