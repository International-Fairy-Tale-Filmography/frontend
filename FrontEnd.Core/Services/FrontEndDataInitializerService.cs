using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Data.Core.Configuration;
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

        _context.Companies.AddRange(await FetchCsv<Company>());
        _context.Countries.AddRange(await FetchCsv<Country>());
        _context.Films.AddRange(await FetchCsv<Film>());
        _context.Languages.AddRange(await FetchCsv<Language>());
        _context.Origins.AddRange(await FetchCsv<Origin>());
        _context.People.AddRange(await FetchCsv<Person>());
        _context.Roles.AddRange(await FetchCsv<Role>());

        await MapFilmCompany();
        await MapFilmCountry();
        await MapFilmLanguage();
        await MapFilmLink();
        await MapFilmOrigin();
        await MapFilmPersonRole();
    }

    private async Task<List<T>> FetchCsv<T>()
    {
        var response = await _httpClient.GetStringAsync($"{_settings.CsvBaseUrl}/{CoreSettings.dbSetNames[typeof(T)]}.csv");

        using var reader = new StringReader(response);

        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

        var records = csv.GetRecords<T>();

        return records.ToList();
    }

    private async Task MapFilmCompany()
    {
        var filmCompanyList = await FetchCsv<FilmCompany>();

        var filmDict = _context.Films.ToDictionary(i => i.Guid, i => i);
        var companyDict = _context.Companies.ToDictionary(i => i.Guid, i => i);

        foreach (var i in filmCompanyList)
        {
            i.Film = filmDict[i.FilmGuid];
            i.Company = companyDict[i.CompanyGuid];
            i.Film.Companies.Add(i);
        }
    }

    private async Task MapFilmLink()
    {
        var filmLinkList = await FetchCsv<FilmLink>();

        var filmDict = _context.Films.ToDictionary(i => i.Guid, i => i);

        foreach (var i in filmLinkList)
        {
            i.Film = filmDict[i.FilmGuid];
            i.Film.Links.Add(i);
        }
    }

    private async Task MapFilmCountry()
    {
        var filmCountryList = await FetchCsv<FilmCountry>();

        var filmDict = _context.Films.ToDictionary(i => i.Guid, i => i);
        var countryDict = _context.Countries.ToDictionary(i => i.Guid, i => i);

        foreach (var i in filmCountryList)
        {
            i.Film = filmDict[i.FilmGuid];
            i.Country = countryDict[i.CountryGuid];
            i.Film.Countries.Add(i);
        }
    }

    private async Task MapFilmLanguage()
    {
        var filmLanguageList = await FetchCsv<FilmLanguage>();

        var filmDict = _context.Films.ToDictionary(i => i.Guid, i => i);
        var languageDict = _context.Languages.ToDictionary(i => i.Guid, i => i);

        foreach (var i in filmLanguageList)
        {
            i.Film = filmDict[i.FilmGuid];

            try
            {
                i.Language = languageDict[i.LanguageGuid];
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error for film {i.FilmGuid}: Language {i.LanguageGuid} not found");
                continue;
            }

            i.Film.Languages.Add(i);

        }
    }

    private async Task MapFilmOrigin()
    {
        var filmOriginList = await FetchCsv<FilmOrigin>();

        var filmDict = _context.Films.ToDictionary(i => i.Guid, i => i);
        var originDict = _context.Origins.ToDictionary(i => i.Guid, i => i);

        foreach (var i in filmOriginList)
        {
            i.Film = filmDict[i.FilmGuid];
            i.Origin = originDict[i.OriginGuid];
            i.Film.Origins.Add(i);
        }
    }

    private async Task MapFilmPersonRole()
    {
        var filmPersonRole = await FetchCsv<FilmPersonRole>();

        var filmDict = _context.Films.ToDictionary(i => i.Guid, i => i);
        var peopleDict = _context.People.ToDictionary(i => i.Guid, i => i);
        var roleDict = _context.Roles.ToDictionary(i => i.Guid, i => i);

        foreach (var i in filmPersonRole)
        {
            i.Film = filmDict[i.FilmGuid];

            try
            {
                i.Person = peopleDict[i.PersonGuid];
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error for film {i.FilmGuid}: Person {i.PersonGuid} not found");
                continue;
            }

            try
            {
                i.Role = roleDict[i.RoleGuid];
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error for film {i.FilmGuid}: Role {i.RoleGuid} not found");
                continue;
            }

          
            i.Film.PeopleRoles.Add(i);
        }
    }

}