using Octokit;
using Microsoft.JSInterop;

//https://www.daveabrock.com/2021/03/14/upload-files-to-github-repository/
public class GitService
{
    private readonly GitHubClient _gitHubClient;
    public CoreSettingsModel _coreSettings;
    private readonly IJSRuntime _jsRuntime;

    public GitService(CoreSettingsModel settings, IJSRuntime jsRuntime)
    {
        _coreSettings = settings;
        _jsRuntime = jsRuntime;

        _gitHubClient = new GitHubClient(new ProductHeaderValue("IFTF"));
    }

    public async Task<CoreSettingsModel> GetConfiguration()
    {

        //var githubAccessToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "github_accesstoken");
        //_coreSettings.AccessToken = githubAccessToken;

        return _coreSettings;
    }

    public async Task SaveConfiguration(CoreSettingsModel settings)
    {
        _coreSettings.AccessToken = settings.AccessToken;
        _coreSettings.Owner = settings.Owner;
        _coreSettings.RepoName = settings.RepoName;
        _coreSettings.Branch = settings.Branch;
        _coreSettings.Folder = settings.Folder;

        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "github_accesstoken", _coreSettings.AccessToken);

        _gitHubClient.Credentials = new Credentials(_coreSettings.AccessToken);
    }

    public async Task<string> GetUser()
    {
        var user = await _gitHubClient.User.Get("thestamp");

        return user.Login;
    }

    public async Task<RepositoryContent> GetFile(string filePath)
    {
        await GetConfiguration();

        
        var fileDetails = await _gitHubClient.Repository.Content.GetAllContentsByRef(
            _coreSettings.Owner, 
            _coreSettings.RepoName,
            filePath, _coreSettings.Branch);

        return fileDetails.First();
    }

    public async Task<RepositoryContentChangeSet> UpdateFile(string filename, RepositoryContent lastCommit, string newContent, string summary)
    {

        var updateResult = await _gitHubClient.Repository.Content.UpdateFile(
            _coreSettings.Owner,
            _coreSettings.RepoName, 
            Path.Combine(_coreSettings.Folder, filename),
            new UpdateFileRequest(summary, newContent, lastCommit.Sha, _coreSettings.Branch));

        return updateResult;
    }
}