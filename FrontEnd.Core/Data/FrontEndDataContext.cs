namespace FrontEnd.Core.Data
{
    public class FrontEndDataContext
    {

        public List<Company> Companies { get; set; } = [];
        public List<Country> Countries { get; set; } = [];
        public List<Film> Films { get; set; } = [];
        public List<Language> Languages { get; set; } = [];
        public List<Origin> Origins { get; set; } = [];
        public List<Person> People { get; set; } = [];
    }
}
