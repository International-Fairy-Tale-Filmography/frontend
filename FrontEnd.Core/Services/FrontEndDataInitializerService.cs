using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Data.Core.Models;
using FrontEnd.Core.Configuration;
using FrontEnd.Core.Data;

namespace FrontEnd.Core.Services;

public class FrontEndDataInitializerService
{
    private readonly FrontEndDataContext _context;
    private readonly HttpClient _httpClient;
    private readonly CoreSettingsModel _settings;

    public FrontEndDataInitializerService(FrontEndDataContext context, HttpClient httpClient, CoreSettingsModel settings)
    {
        _context = context;
        _httpClient = httpClient;
        _settings = settings;
    }
    public async Task FetchData()
    {
        if (_context.Companies.Any())
        {
            return;
        }

        _context.Companies.AddRange(await FetchCsv<Company>("Companies"));
        _context.Countries.AddRange(await FetchCsv<Country>("Countries"));
        _context.Films.AddRange(await FetchCsv<Film>("Films"));
        _context.Languages.AddRange(await FetchCsv<Language>("Languages"));
        _context.Origins.AddRange(await FetchCsv<Origin>("Origins"));
        _context.People.AddRange(await FetchCsv<Person>("People"));


        //await MapCompanyFilms();
        //await MapCountryFilms();
    }

    private async Task<List<T>> FetchCsv<T>(string name)
    {
        var response = await _httpClient.GetStringAsync($"{_settings.CsvBaseUrl}/{name}.csv");

        using var reader = new StringReader(response);

        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));


        var records = csv.GetRecords<T>();

        return records.ToList();
    }

    //private async Task MapCompanyFilms()
    //{
    //    //var companyFilms = await FetchCsv<FilmCompany>("CompanyFilm");

    //    //var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
    //    //var companyDict = _context.Companies.ToDictionary(i => i.CompanyId, i => i);

    //    //foreach (var i in companyFilms)
    //    //{
    //    //    var film = filmDict[i.FilmId];

    //    //    var company = companyDict[i.CompanyId];

    //    //    film.Companies.Add(company);
    //    //    company.Films.Add(film);
    //    //}

    //}


    //private async Task MapCountryFilms()
    //{
    //    //var companyFilms = await FetchCsv<FilmCountry>("CountryFilm");

    //    //var filmDict = _context.Films.ToDictionary(i => i.FilmId, i => i);
    //    //var countryDict = _context.Countries.ToDictionary(i => i.CountryId, i => i);

    //    //foreach (var i in companyFilms)
    //    //{
    //    //    var film = filmDict[i.FilmId];

    //    //    var country = countryDict[i.CountryId];

    //    //    film.Countries.Add(country);
    //    //    country.Films.Add(film);
    //    //}

    //}



}