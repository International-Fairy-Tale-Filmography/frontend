using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper;
using Data.Core;

public class DataSourceService
{
    private readonly BaseDataContext _context;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public DataSourceService(BaseDataContext context, HttpClient httpClient, string baseUrl)
    {
        _context = context;
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }
    public async Task FetchData()
    {

        _context.Companies.AddRange(await FetchCsv<Company>("Company"));
        _context.Countries.AddRange(await FetchCsv<Country>("Country"));
        _context.Films.AddRange(await FetchCsv<Film>("Film"));
        _context.Languages.AddRange(await FetchCsv<Language>("Language"));
        _context.Origins.AddRange(await FetchCsv<Origin>("Origin"));
        _context.People.AddRange(await FetchCsv<Person>("Person"));


        await MapCompanyFilms();
        await MapCountryFilms();
    }

    private async Task<List<T>> FetchCsv<T>(string name)
    {
        var response = await _httpClient.GetStringAsync($"{_baseUrl}/{name}.csv");

        using var reader = new StringReader(response);

        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));


        var records = csv.GetRecords<T>();

        return records.ToList();
    }

    private async Task MapCompanyFilms()
    {
        var companyFilms = await FetchCsv<CompanyFilm>("CompanyFilm");

        var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
        var companyDict = _context.Companies.ToDictionary(i => i.CompanyId, i => i);

        foreach (var i in companyFilms)
        {
            var film = filmDict[i.FilmId];

            var company = companyDict[i.CompanyId];

            film.Companies.Add(company);
            company.Films.Add(film);
        }

    }


    private async Task MapCountryFilms()
    {
        var companyFilms = await FetchCsv<CountryFilm>("CountryFilm");

        var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
        var countryDict = _context.Countries.ToDictionary(i => i.CountryId, i => i);

        foreach (var i in companyFilms)
        {
            var film = filmDict[i.FilmId];

            var country = countryDict[i.CountryId];

            film.Countries.Add(country);
            country.Films.Add(film);
        }

    }



}

