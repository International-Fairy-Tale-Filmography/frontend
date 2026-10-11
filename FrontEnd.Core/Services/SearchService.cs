using Data.Core.Configuration;
using Data.Core.Models;
using FrontEnd.Core.Data;

namespace FrontEnd.Core.Services;

public enum FilmSortOption
{
    TitleAsc,
    YearAsc,
    YearDesc
}

public class FilmSearchQuery
{
    public string? Title { get; set; }
    public Guid? DirectorGuid { get; set; }
    public Guid? CountryGuid { get; set; }
    public Guid? CompanyGuid { get; set; }
    public Guid? PersonGuid { get; set; }
    public Guid? OriginGuid { get; set; }
    public Guid? LanguageGuid { get; set; }
    public int? Year { get; set; }
    public int? DurationMin { get; set; }
    public int? DurationMax { get; set; }
    public string? Mode { get; set; }
    public FilmSortOption Sort { get; set; } = FilmSortOption.TitleAsc;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
}

public class FilmSearchResult
{
    public required IReadOnlyList<Film> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public bool HasMore => Page * PageSize < TotalCount;
}

public class SearchService
{
    private readonly FrontEndDataContext _context;

    public SearchService(FrontEndDataContext context)
    {
        _context = context;
    }

    public FilmSearchResult Search(FilmSearchQuery query)
    {
        var normalizedQuery = NormalizeQuery(query);

        IEnumerable<Film> filmQuery = _context.Films;

        if (!string.IsNullOrWhiteSpace(normalizedQuery.Title))
        {
            var title = normalizedQuery.Title.Trim();
            filmQuery = filmQuery.Where(f =>
                (!string.IsNullOrWhiteSpace(f.Title) && f.Title.Contains(title, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(f.OtherTitle) && f.OtherTitle.Contains(title, StringComparison.OrdinalIgnoreCase)));
        }

        if (normalizedQuery.DirectorGuid.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.PeopleRoles.Any(pr =>
                pr.PersonGuid == normalizedQuery.DirectorGuid.Value &&
                pr.RoleGuid == CoreSettings.DirectorRoleId));
        }

        if (normalizedQuery.CountryGuid.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.Countries.Any(c => c.CountryGuid == normalizedQuery.CountryGuid.Value));
        }

        if (normalizedQuery.CompanyGuid.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.Companies.Any(c => c.CompanyGuid == normalizedQuery.CompanyGuid.Value));
        }

        if (normalizedQuery.PersonGuid.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.PeopleRoles.Any(pr => pr.PersonGuid == normalizedQuery.PersonGuid.Value));
        }

        if (normalizedQuery.OriginGuid.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.Origins.Any(o => o.OriginGuid == normalizedQuery.OriginGuid.Value));
        }

        if (normalizedQuery.LanguageGuid.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.Languages.Any(l => l.LanguageGuid == normalizedQuery.LanguageGuid.Value));
        }

        if (normalizedQuery.Year.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.ReleaseYear == normalizedQuery.Year.Value);
        }

        if (normalizedQuery.DurationMin.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.Duration.HasValue && f.Duration.Value >= normalizedQuery.DurationMin.Value);
        }

        if (normalizedQuery.DurationMax.HasValue)
        {
            filmQuery = filmQuery.Where(f => f.Duration.HasValue && f.Duration.Value <= normalizedQuery.DurationMax.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.Mode))
        {
            filmQuery = filmQuery.Where(f => string.Equals(f.Mode, normalizedQuery.Mode, StringComparison.OrdinalIgnoreCase));
        }

        filmQuery = normalizedQuery.Sort switch
        {
            FilmSortOption.YearAsc => filmQuery
                .OrderBy(f => f.ReleaseYear.HasValue ? 0 : 1)
                .ThenBy(f => f.ReleaseYear)
                .ThenBy(f => f.Title),
            FilmSortOption.YearDesc => filmQuery
                .OrderBy(f => f.ReleaseYear.HasValue ? 0 : 1)
                .ThenByDescending(f => f.ReleaseYear)
                .ThenBy(f => f.Title),
            _ => filmQuery
                .OrderBy(f => f.Title)
                .ThenBy(f => f.ReleaseYear)
        };

        var totalCount = filmQuery.Count();

        var pageItems = filmQuery
            .Skip((normalizedQuery.Page - 1) * normalizedQuery.PageSize)
            .Take(normalizedQuery.PageSize)
            .ToList();

        return new FilmSearchResult
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = normalizedQuery.Page,
            PageSize = normalizedQuery.PageSize
        };
    }

    private static FilmSearchQuery NormalizeQuery(FilmSearchQuery query)
    {
        return new FilmSearchQuery
        {
            Title = query.Title,
            DirectorGuid = query.DirectorGuid,
            CountryGuid = query.CountryGuid,
            CompanyGuid = query.CompanyGuid,
            PersonGuid = query.PersonGuid,
            OriginGuid = query.OriginGuid,
            LanguageGuid = query.LanguageGuid,
            Year = query.Year,
            DurationMin = query.DurationMin,
            DurationMax = query.DurationMax,
            Mode = query.Mode,
            Sort = query.Sort,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize switch
            {
                < 1 => 30,
                > 200 => 200,
                _ => query.PageSize
            }
        };
    }
}
