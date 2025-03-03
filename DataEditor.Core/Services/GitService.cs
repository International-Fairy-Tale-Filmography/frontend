// File: DataEditor.Core/Services/GitService.cs
using Octokit;
using Microsoft.JSInterop;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
}

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
        var githubAccessToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "github_accesstoken");
        _coreSettings.AccessToken = githubAccessToken;
        _gitHubClient.Credentials = new Credentials(_coreSettings.AccessToken);

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
        
        await GetConfiguration();

        var user = await _gitHubClient.User.Current();

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
        await GetConfiguration();

        var updateResult = await _gitHubClient.Repository.Content.UpdateFile(
            _coreSettings.Owner,
            _coreSettings.RepoName,
            Path.Combine(_coreSettings.Folder, filename),
            new UpdateFileRequest(summary, newContent, lastCommit.Sha, _coreSettings.Branch));

        return updateResult;
    }

    public async Task<List<string>> GetBranches()
    {
        await GetConfiguration();

        var branches = await _gitHubClient.Repository.Branch.GetAll(_coreSettings.Owner, _coreSettings.RepoName);
        return branches
            .Where(b => !b.Name.StartsWith("gh-pages-"))
            .Select(b => b.Name)
            .ToList();
    }

    /// <summary>
    /// Validates the provided GitHub access token by attempting to retrieve the authenticated user.
    /// Ensures the token has the necessary permissions.
    /// </summary>
    /// <param name="token">GitHub access token to validate.</param>
    /// <returns>A ValidationResult indicating success or failure with an error message.</returns>
    public async Task<ValidationResult> ValidateToken(string token)
    {

        try
        {
            var testClient = new GitHubClient(new ProductHeaderValue("IFTF"))
            {
                Credentials = new Credentials(token)
            };

            // Attempt to get the current user
            var user = await testClient.User.Current();

            // Check if the token has 'repo' scope by attempting to list repositories
            var userRepos = await testClient.Repository.GetAllForCurrent();

            // Additional permission checks can be added here if necessary

            return new ValidationResult { IsValid = true };
        }
        catch (AuthorizationException)
        {
            // Token is invalid or lacks necessary permissions
            return new ValidationResult { IsValid = false, ErrorMessage = "Invalid token or insufficient permissions. Please ensure the token has 'public_repo' access." };
        }
        catch (Exception ex)
        {
            // Handle other exceptions if necessary
            return new ValidationResult { IsValid = false, ErrorMessage = $"An error occurred while validating the token: {ex.Message}" };
        }
    }
}
