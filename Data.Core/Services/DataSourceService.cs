using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper;

public class DataSourceService
{
    private readonly BasicDataContext _context;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public DataSourceService(BasicDataContext context, HttpClient httpClient, string baseUrl)
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


        await MapCompanyFilms(_context);
        await MapCountryFilms(_context);
    }

    private async Task<List<T>> FetchCsv<T>(string name)
    {
        var response = await _httpClient.GetStringAsync($"{_baseUrl}/{name}.csv");

        using var reader = new StringReader(response);

        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));


        var records = csv.GetRecords<T>();

        return records.ToList();
    }

    private async Task MapCompanyFilms(BasicDataContext context)
    {
        var companyFilms = await FetchCsv<CompanyFilm>("CompanyFilm");

        var filmDict = context.Films.ToDictionary(i => i.FilmId, i => i);
        var companyDict = context.Companies.ToDictionary(i => i.CompanyId, i => i);

        foreach (var i in companyFilms)
        {
            var film = filmDict[i.FilmId];

            var company = companyDict[i.CompanyId];

            film.Companies.Add(company);
            company.Films.Add(film);
        }

    }


    private async Task MapCountryFilms(BasicDataContext context)
    {
        var companyFilms = await FetchCsv<CountryFilm>("CountryFilm");

        var filmDict = context.Films.ToDictionary(i => i.FilmId, i => i);
        var countryDict = context.Countries.ToDictionary(i => i.CountryId, i => i);

        foreach (var i in companyFilms)
        {
            var film = filmDict[i.FilmId];

            var country = countryDict[i.CountryId];

            film.Countries.Add(country);
            country.Films.Add(film);
        }

    }



}

