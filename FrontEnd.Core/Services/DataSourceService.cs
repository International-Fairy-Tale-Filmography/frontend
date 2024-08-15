using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using CsvHelper;
using FrontEnd.Core.Data;
using FrontEnd.Core.Data.Models;

namespace FrontEnd.Core.Services
{
    public class DataSourceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public DataSourceService(HttpClient httpClient, string baseUrl)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl;
        }
        public async Task FetchData(DataContext context)
        {
            
            context.Companies.AddRange(await FetchCsv<Company>("Company"));
            context.Countries.AddRange(await FetchCsv<Country>("Country"));
            context.Films.AddRange(await FetchCsv<Film>("Film"));
            context.Languages.AddRange(await FetchCsv<Language>("Language"));
            context.Origins.AddRange(await FetchCsv<Origin>("Origin"));
            context.People.AddRange(await FetchCsv<Person>("Person"));

        }

        private async Task<List<T>> FetchCsv<T>(string name)
        {
            var response = await _httpClient.GetStringAsync($"{_baseUrl}/{name}.csv");

            using var reader = new StringReader(response);

            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));


            var records = csv.GetRecords<T>();

            return records.ToList();
        }


    }
    
}
