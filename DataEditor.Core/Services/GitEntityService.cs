using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
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
        private readonly SemaphoreSlim _dataVersionLock = new(1, 1);
        private readonly Dictionary<string, string> _draftContents = new(StringComparer.OrdinalIgnoreCase);
        private string _dataVersionSha;
        private bool _draftLoaded;

        public string DataVersionSha => _dataVersionSha;
        public bool HasRecoverableDraft { get; private set; }

        public GitEntityService(DataEditorDataContext context, CoreSettingsModel settings, GitService gitService)
        {
            _context = context;
            _settings = settings;
            _gitService = gitService;
        }

        public sealed class StaleDataException : Exception
        {
            public StaleDataException(string baselineSha, string latestSha)
                : base("This editing session is based on an older version of the selected data set. Reload or review the newer data before publishing.")
            {
                BaselineSha = baselineSha;
                LatestSha = latestSha;
            }

            public string BaselineSha { get; }
            public string LatestSha { get; }
        }

        private sealed record FileChange(string FileName, string Content);
        private sealed class DraftSnapshot
        {
            public string Branch { get; set; }
            public string BaseSha { get; set; }
            public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<string> CommitAllChangesToGit(Action<string> progressCallback = null)
        {
            await EnsureBranchIsCurrentAsync();
            var fileChanges = new List<FileChange?>
            {
                await GetFileChange(_context.Countries.OrderBy(i => i.Guid).ToList()),
                await GetFileChange(_context.Films.OrderBy(i => i.Guid).ToList()),
                await GetFileChange(_context.Origins.OrderBy(i => i.Guid).ToList()),
                await GetFileChange(_context.People.OrderBy(i => i.Guid).ToList()),
                await GetFileChange(_context.Roles.OrderBy(i => i.Guid).ToList()),
                await GetFileChange(_context.Languages.OrderBy(i => i.Guid).ToList()),
                await GetFileChange(_context.Companies.OrderBy(i => i.Guid).ToList()),
                await GetFileChange(_context.FilmLinks.OrderBy(i => i.Guid).ThenBy(i => i.FilmGuid).ToList()),
                await GetFileChange(_context.FilmCompanies.OrderBy(i => i.FilmGuid).ThenBy(i => i.CompanyGuid).ToList()),
                await GetFileChange(_context.FilmCountries.OrderBy(i => i.FilmGuid).ThenBy(i => i.CountryGuid).ToList()),
                await GetFileChange(_context.FilmLanguages.OrderBy(i => i.FilmGuid).ThenBy(i => i.LanguageGuid).ToList()),
                await GetFileChange(_context.FilmOrigins.OrderBy(i => i.FilmGuid).ThenBy(i => i.OriginGuid).ToList()),
                await GetFileChange(_context.FilmPersonRoles.OrderBy(i => i.Film).ThenBy(i => i.Person).ThenBy(i => i.RoleGuid).ToList())
            };

            var changedFiles = fileChanges
                .OfType<FileChange>()
                .ToDictionary(change => change.FileName, change => change.Content);

            if (changedFiles.Count == 0)
            {
                return "There are no saved changes to publish.";
            }

            progressCallback?.Invoke($"Preparing {changedFiles.Count} changed data file(s)...");
            await PublishChangesAtomically(changedFiles);
            progressCallback?.Invoke("All changed data files were published together.");
            await ResetCaches();
            return string.Join("; ", changedFiles.Keys);
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

        public async Task<List<FileDifference>> GetLatestVersionDifferences()
        {
            await EnsureDataVersionCaptured();
            var latestSha = await _gitService.GetBranchHeadSha();
            if (string.Equals(_dataVersionSha, latestSha, StringComparison.Ordinal))
            {
                return new List<FileDifference>();
            }

            var differences = new List<FileDifference>();
            differences.Add(await GetLatestVersionDifference<Country>(latestSha));
            differences.Add(await GetLatestVersionDifference<Film>(latestSha));
            differences.Add(await GetLatestVersionDifference<Origin>(latestSha));
            differences.Add(await GetLatestVersionDifference<Person>(latestSha));
            differences.Add(await GetLatestVersionDifference<Role>(latestSha));
            differences.Add(await GetLatestVersionDifference<Language>(latestSha));
            differences.Add(await GetLatestVersionDifference<Company>(latestSha));
            differences.Add(await GetLatestVersionDifference<FilmLink>(latestSha));
            differences.Add(await GetLatestVersionDifference<FilmCompany>(latestSha));
            differences.Add(await GetLatestVersionDifference<FilmCountry>(latestSha));
            differences.Add(await GetLatestVersionDifference<FilmLanguage>(latestSha));
            differences.Add(await GetLatestVersionDifference<FilmOrigin>(latestSha));
            differences.Add(await GetLatestVersionDifference<FilmPersonRole>(latestSha));
            return differences;
        }

        private async Task<FileDifference> GetLatestVersionDifference<T>(string latestSha)
        {
            var filename = $"{CoreSettings.dbSetNames[typeof(T)]}.csv";
            var difference = new FileDifference { FileName = filename, OldContent = string.Empty, NewContent = string.Empty };
            if (!LoadedFiles.Contains(typeof(T)))
            {
                return difference;
            }

            var path = Path.Combine(_settings.Folder, filename);
            var originalFile = await _gitService.GetFile(path, _dataVersionSha);
            var latestFile = await _gitService.GetFile(path, latestSha);
            difference.OldContent = await GetFileContentFromDownloadUrl(originalFile);
            difference.NewContent = await GetFileContentFromDownloadUrl(latestFile);
            difference.RecordDifferences = BuildRecordDifferences(filename, difference.OldContent, difference.NewContent);
            difference.ChangedLinesCount = difference.RecordDifferences.Count;
            return difference;
        }

        private static List<RecordDifference> BuildRecordDifferences(string fileName, string oldContent, string newContent)
        {
            var keyFields = GetKeyFields(fileName);
            var oldRecords = ParseCsvRecords(oldContent);
            var newRecords = ParseCsvRecords(newContent);
            var oldByKey = IndexRecords(oldRecords, keyFields, fileName);
            var newByKey = IndexRecords(newRecords, keyFields, fileName);
            var differences = new List<RecordDifference>();

            foreach (var key in oldByKey.Keys.Union(newByKey.Keys).OrderBy(value => value, StringComparer.Ordinal))
            {
                oldByKey.TryGetValue(key, out var oldRecord);
                newByKey.TryGetValue(key, out var newRecord);
                if (oldRecord == null)
                {
                    differences.Add(CreateRecordDifference(fileName, key, newRecord, "Added"));
                }
                else if (newRecord == null)
                {
                    differences.Add(CreateRecordDifference(fileName, key, oldRecord, "Removed"));
                }
                else
                {
                    var changedFields = oldRecord.Keys.Union(newRecord.Keys, StringComparer.OrdinalIgnoreCase)
                        .Where(field => !string.Equals(GetValue(oldRecord, field), GetValue(newRecord, field), StringComparison.Ordinal))
                        .Select(field => new FieldDifference
                        {
                            FieldName = ToDisplayName(field),
                            OldValue = GetValue(oldRecord, field),
                            NewValue = GetValue(newRecord, field)
                        })
                        .ToList();

                    if (changedFields.Count > 0)
                    {
                        differences.Add(new RecordDifference
                        {
                            RecordLabel = GetRecordLabel(fileName, newRecord, key),
                            ChangeType = "Changed",
                            Fields = changedFields
                        });
                    }
                }
            }

            return differences;
        }

        private static Dictionary<string, string>[] ParseCsvRecords(string content)
        {
            using var reader = new StringReader(content);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                HeaderValidated = null
            });

            if (!csv.Read())
            {
                return Array.Empty<Dictionary<string, string>>();
            }

            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            var records = new List<Dictionary<string, string>>();
            while (csv.Read())
            {
                var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < headers.Length; index++)
                {
                    record[headers[index]] = csv.GetField(index) ?? string.Empty;
                }

                records.Add(record);
            }

            return records.ToArray();
        }

        private static Dictionary<string, Dictionary<string, string>> IndexRecords(
            IEnumerable<Dictionary<string, string>> records,
            string[] keyFields,
            string fileName)
        {
            var indexed = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                var missingFields = keyFields.Where(field => !record.ContainsKey(field)).ToArray();
                if (missingFields.Length > 0)
                {
                    throw new InvalidDataException($"Cannot compare {fileName}: required record key field(s) are missing: {string.Join(", ", missingFields)}.");
                }

                var key = string.Join("|", keyFields.Select(field => record[field]));
                if (!indexed.TryAdd(key, record))
                {
                    throw new InvalidDataException($"Cannot compare {fileName}: record key '{key}' appears more than once.");
                }
            }

            return indexed;
        }

        private static RecordDifference CreateRecordDifference(
            string fileName,
            string key,
            Dictionary<string, string> record,
            string changeType)
        {
            return new RecordDifference
            {
                RecordLabel = GetRecordLabel(fileName, record, key),
                ChangeType = changeType,
                Fields = record.Select(field => new FieldDifference
                {
                    FieldName = ToDisplayName(field.Key),
                    OldValue = changeType == "Removed" ? field.Value : string.Empty,
                    NewValue = changeType == "Added" ? field.Value : string.Empty
                }).ToList()
            };
        }

        private static string[] GetKeyFields(string fileName) => fileName switch
        {
            "FilmCompanies.csv" => new[] { "FilmGuid", "CompanyGuid" },
            "FilmCountries.csv" => new[] { "FilmGuid", "CountryGuid" },
            "FilmLanguages.csv" => new[] { "FilmGuid", "LanguageGuid" },
            "FilmOrigins.csv" => new[] { "FilmGuid", "OriginGuid" },
            "FilmPersonRoles.csv" => new[] { "FilmGuid", "PersonGuid", "RoleGuid" },
            _ => new[] { "Guid" }
        };

        private static string GetRecordLabel(string fileName, Dictionary<string, string> record, string key)
        {
            var displayFields = fileName switch
            {
                "Films.csv" => new[] { "Title" },
                "Countries.csv" or "Companies.csv" or "Languages.csv" or "Roles.csv" => new[] { "Name" },
                "Origins.csv" => new[] { "Title" },
                "People.csv" => new[] { "FirstName", "LastName" },
                "FilmLinks.csv" => new[] { "Url" },
                _ => Array.Empty<string>()
            };
            var label = string.Join(" ", displayFields.Select(field => GetValue(record, field)).Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(label) ? key : label;
        }

        private static string GetValue(Dictionary<string, string> record, string field)
        {
            return record.TryGetValue(field, out var value) ? value : string.Empty;
        }

        private static string ToDisplayName(string fieldName)
        {
            return System.Text.RegularExpressions.Regex.Replace(fieldName, "(?<!^)([A-Z])", " $1");
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

            var file = await GetFileByName(filename);

            await using var writer = new StringWriter();
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
            csv.WriteHeader<T>();
            await csv.NextRecordAsync();
            await csv.WriteRecordsAsync(objects);

            var oldContent = await GetFileContentFromDownloadUrl(file);
            var newContent = writer.ToString();

            fileDiff.OldContent = oldContent;
            fileDiff.NewContent = newContent;
            fileDiff.RecordDifferences = BuildRecordDifferences(filename, oldContent, newContent);
            fileDiff.ChangedLinesCount = fileDiff.RecordDifferences.Count;
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
            await EnsureBranchIsCurrentAsync();
            var change = await GetFileChange(objects);
            if (change == null)
            {
                return string.Empty;
            }

            await PublishChangesAtomically(new Dictionary<string, string> { [change.FileName] = change.Content });
            return change.FileName;
        }

        private async Task<FileChange?> GetFileChange<T>(List<T> objects)
        {
            if (!LoadedFiles.Contains(typeof(T)))
            {
                return null;
            }

            var filename = $"{CoreSettings.dbSetNames[typeof(T)]}.csv";
            var file = await GetFileByName(filename);
            var oldContent = await GetFileContentFromDownloadUrl(file);

            await using var writer = new StringWriter();
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
            csv.WriteHeader<T>();
            await csv.NextRecordAsync();
            await csv.WriteRecordsAsync(objects);

            var newContent = writer.ToString();
            return oldContent == newContent ? null : new FileChange(filename, newContent);
        }

        private async Task PublishChangesAtomically(IReadOnlyDictionary<string, string> changedFiles)
        {
            try
            {
                await _gitService.UpdateFilesAtomically(changedFiles, _dataVersionSha, "Update data editor records");
            }
            catch (GitService.BranchMovedException ex)
            {
                throw new StaleDataException(_dataVersionSha, ex.LatestSha);
            }
        }


        public async Task<RepositoryContent> GetFileByName(string name)
        {
            await EnsureDataVersionCaptured();
            var filePath = Path.Combine(_settings.Folder, name);
            var file = await _gitService.GetFile(filePath, _dataVersionSha);

            return file;
        }

        public Task<string> GetLatestBranchHeadSha()
        {
            return _gitService.GetBranchHeadSha();
        }

        public async Task EnsureBranchIsCurrentAsync()
        {
            await EnsureDataVersionCaptured();
            var latestSha = await _gitService.GetBranchHeadSha();
            if (!string.Equals(_dataVersionSha, latestSha, StringComparison.Ordinal))
            {
                throw new StaleDataException(_dataVersionSha, latestSha);
            }
        }

        private async Task EnsureDataVersionCaptured()
        {
            if (!string.IsNullOrEmpty(_dataVersionSha))
            {
                return;
            }

            await _dataVersionLock.WaitAsync();
            try
            {
                if (!_draftLoaded)
                {
                    var savedDraft = await _gitService.GetEditorDraft(_settings.Branch);
                    if (!string.IsNullOrWhiteSpace(savedDraft))
                    {
                        var snapshot = JsonSerializer.Deserialize<DraftSnapshot>(savedDraft)
                            ?? throw new InvalidDataException("The saved editing-session draft could not be read.");
                        if (!string.Equals(snapshot.Branch, _settings.Branch, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException("The saved editing-session draft belongs to a different data set.");
                        }

                        if (string.IsNullOrWhiteSpace(snapshot.BaseSha) || snapshot.Files == null)
                        {
                            throw new InvalidDataException("The saved editing-session draft is incomplete.");
                        }

                        _dataVersionSha = snapshot.BaseSha;
                        foreach (var file in snapshot.Files)
                        {
                            _draftContents[file.Key] = file.Value;
                        }

                        HasRecoverableDraft = _draftContents.Count > 0;
                    }

                    _draftLoaded = true;
                }

                if (string.IsNullOrEmpty(_dataVersionSha))
                {
                    _dataVersionSha = await _gitService.GetBranchHeadSha();
                }
            }
            finally
            {
                _dataVersionLock.Release();
            }
        }

        public static HashSet<Type> LoadedFiles = new ();


        // Modify the SeedDataFromGit method to accept a progress callback
        public async Task SeedDataFromGit<T>(Action<string> progressCallback = null) where T : class
        {
            if (!LoadedFiles.Contains(typeof(T)))
            {
                var fileName = $"{CoreSettings.dbSetNames[typeof(T)]}.csv";
                progressCallback?.Invoke($"Loading {fileName}...");

                var entities = await FetchCsv<T>(fileName);

                await BulkInsertAsync(entities);

                //mark the file as loaded
                LoadedFiles.Add(typeof(T));
            }

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

        private async Task<List<T>> FetchCsv<T>(string name)
        {
            await EnsureDataVersionCaptured();
            string content;
            if (!_draftContents.TryGetValue(name, out content))
            {
                var repositoryFile = await GetFileByName(name);
                content = await GetFileContentFromDownloadUrl(repositoryFile);
            }

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

        // Add a new method to load only Film data initially
        public async Task SeedFilmDataOnly(Action<string> progressCallback = null)
        {
            progressCallback?.Invoke("Loading Films...");

            if (!LoadedFiles.Contains(typeof(Film)))
            {
                var fileName = $"{CoreSettings.dbSetNames[typeof(Film)]}.csv";
                progressCallback?.Invoke($"Loading {fileName}...");

                var entities = await FetchCsv<Film>(fileName);

                await BulkInsertAsync(entities);

                LoadedFiles.Add(typeof(Film));
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

        public class FileDifference
        {
            public string FileName { get; set; }
            public string OldContent { get; set; }
            public string NewContent { get; set; }
            public int ChangedLinesCount { get; set; }
            public List<RecordDifference> RecordDifferences { get; set; } = new();
        }

        public class RecordDifference
        {
            public string RecordLabel { get; set; }
            public string ChangeType { get; set; }
            public List<FieldDifference> Fields { get; set; } = new();
        }

        public class FieldDifference
        {
            public string FieldName { get; set; }
            public string OldValue { get; set; }
            public string NewValue { get; set; }
        }

        public async Task SaveDraftAsync(params Type[] changedEntityTypes)
        {
            await EnsureDataVersionCaptured();
            foreach (var entityType in changedEntityTypes.Distinct())
            {
                var fileName = $"{CoreSettings.dbSetNames[entityType]}.csv";
                var content = await SerializeCurrentTable(entityType);
                if (content != null)
                {
                    _draftContents[fileName] = content;
                }
            }

            var snapshot = new DraftSnapshot
            {
                Branch = _settings.Branch,
                BaseSha = _dataVersionSha,
                Files = new Dictionary<string, string>(_draftContents, StringComparer.OrdinalIgnoreCase)
            };
            await _gitService.SaveEditorDraft(_settings.Branch, JsonSerializer.Serialize(snapshot));
            HasRecoverableDraft = _draftContents.Count > 0;
        }

        private Task<string> SerializeCurrentTable(Type entityType)
        {
            return entityType == typeof(Company) ? SerializeCsv(_context.Companies.OrderBy(item => item.Guid).ToList())
                : entityType == typeof(Country) ? SerializeCsv(_context.Countries.OrderBy(item => item.Guid).ToList())
                : entityType == typeof(Film) ? SerializeCsv(_context.Films.OrderBy(item => item.Guid).ToList())
                : entityType == typeof(Language) ? SerializeCsv(_context.Languages.OrderBy(item => item.Guid).ToList())
                : entityType == typeof(Origin) ? SerializeCsv(_context.Origins.OrderBy(item => item.Guid).ToList())
                : entityType == typeof(Person) ? SerializeCsv(_context.People.OrderBy(item => item.Guid).ToList())
                : entityType == typeof(Role) ? SerializeCsv(_context.Roles.OrderBy(item => item.Guid).ToList())
                : entityType == typeof(FilmLink) ? SerializeCsv(_context.FilmLinks.OrderBy(item => item.Guid).ThenBy(item => item.FilmGuid).ToList())
                : entityType == typeof(FilmCompany) ? SerializeCsv(_context.FilmCompanies.OrderBy(item => item.FilmGuid).ThenBy(item => item.CompanyGuid).ToList())
                : entityType == typeof(FilmCountry) ? SerializeCsv(_context.FilmCountries.OrderBy(item => item.FilmGuid).ThenBy(item => item.CountryGuid).ToList())
                : entityType == typeof(FilmLanguage) ? SerializeCsv(_context.FilmLanguages.OrderBy(item => item.FilmGuid).ThenBy(item => item.LanguageGuid).ToList())
                : entityType == typeof(FilmOrigin) ? SerializeCsv(_context.FilmOrigins.OrderBy(item => item.FilmGuid).ThenBy(item => item.OriginGuid).ToList())
                : entityType == typeof(FilmPersonRole) ? SerializeCsv(_context.FilmPersonRoles.OrderBy(item => item.FilmGuid).ThenBy(item => item.PersonGuid).ThenBy(item => item.RoleGuid).ToList())
                : throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unsupported draft table.");
        }

        private static async Task<string> SerializeCsv<T>(List<T> records)
        {
            await using var writer = new StringWriter();
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
            csv.WriteHeader<T>();
            await csv.NextRecordAsync();
            await csv.WriteRecordsAsync(records);
            return writer.ToString();
        }

        public async Task ClearDraftAsync()
        {
            await _gitService.ClearEditorDraft(_settings.Branch);
            _draftContents.Clear();
            HasRecoverableDraft = false;
        }

        public async Task ResetCaches()
        {
            await ClearDraftAsync();
            // Clear all DbSets by removing all entities and resetting their state
            _context.ClearAllDbSets();

            // Detach all tracked entities to avoid tracking conflicts
            _context.DetachAllEntities();

            // Clear the LoadedFiles collection to force reloading all data from Git
            LoadedFiles.Clear();
            _draftContents.Clear();
            _draftLoaded = false;
            _dataVersionSha = null;
        }
    }
}
