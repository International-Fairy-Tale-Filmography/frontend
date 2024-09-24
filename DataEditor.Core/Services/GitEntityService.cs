using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Octokit;

namespace DataEditor.Core.Services
{
    public class GitEntityService(DataEditorDataContext context, CoreSettingsModel settings, GitService gitService)
    {

        public async Task SaveChangesToGit()
        {
            var films = await context.Films.ToListAsync();

            await using var writer = new StringWriter();
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
            await csv.WriteRecordsAsync(films);
        }


        public async Task FetchDataFromGit()
        {
            context.Companies.AddRange(await FetchCsv<Company>("Company"));
            context.Countries.AddRange(await FetchCsv<Country>("Country"));
            context.Films.AddRange(await FetchCsv<Film>("Film"));
            context.Languages.AddRange(await FetchCsv<Language>("Language"));
            context.Origins.AddRange(await FetchCsv<Origin>("Origin"));
            context.People.AddRange(await FetchCsv<Person>("Person"));

            await context.SaveChangesAsync();
        }


        private async Task<List<T>> FetchCsv<T>(string name)
        {
            var filePath = Path.Combine(settings.Folder, name) + ".csv";
            var file = await gitService.GetFile(filePath);

            using var reader = new StringReader(file.Content);

            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            var records = csv.GetRecords<T>();

            return records.ToList();
        }

    }
}
