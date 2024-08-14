using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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

        public DataSourceService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task FetchData(DataContext context)
        {
            var response = await _httpClient.GetStringAsync("/sample-data/film.csv");

            using var reader = new StringReader(response);
            
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            
            
            var records = csv.GetRecords<Film>();

            context.Films = records.ToList();
            
        }


    }
    
}
