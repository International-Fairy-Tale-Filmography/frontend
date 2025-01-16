using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Data.Core.Models;
using FrontEnd.Core.Data;

namespace FrontEnd.Core.Services;

public class FrontEndDataInitializerService(FrontEndDataContext context, HttpClient httpClient, string baseUrl)
{
    public async Task FetchData()
    {


        context.Companies.AddRange(await FetchCsv<Company>("Company"));
        context.Countries.AddRange(await FetchCsv<Country>("Country"));
        context.Films.AddRange(await FetchCsv<Film>("Film"));
        context.Languages.AddRange(await FetchCsv<Language>("Language"));
        context.Origins.AddRange(await FetchCsv<Origin>("Origin"));
        context.People.AddRange(await FetchCsv<Person>("Person"));


        await MapCompanyFilms();
        await MapCountryFilms();
    }

    private async Task<List<T>> FetchCsv<T>(string name)
    {
        var response = await httpClient.GetStringAsync($"{baseUrl}/{name}.csv");

        using var reader = new StringReader(response);

        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));


        var records = csv.GetRecords<T>();

        return records.ToList();
    }

    private async Task MapCompanyFilms()
    {
        var companyFilms = await FetchCsv<FilmCompany>("CompanyFilm");

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


    private async Task MapCountryFilms()
    {
        var companyFilms = await FetchCsv<FilmCountry>("CountryFilm");

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