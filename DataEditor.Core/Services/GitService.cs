// File: DataEditor.Core/Services/GitService.cs
using System.Net;
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
        var selectedBranch = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "data_editor_branch");
        _coreSettings.AccessToken = githubAccessToken;
        if (!string.IsNullOrWhiteSpace(selectedBranch))
        {
            _coreSettings.Branch = selectedBranch;
        }

        if (!string.IsNullOrEmpty(_coreSettings.AccessToken))
        {
            _gitHubClient.Credentials = new Credentials(_coreSettings.AccessToken);
        }

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
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "data_editor_branch", _coreSettings.Branch);

        _gitHubClient.Credentials = new Credentials(_coreSettings.AccessToken);
    }

    public async Task<string> GetUser()
    {
        
        await GetConfiguration();

        var user = await _gitHubClient.User.Current();

        return user.Login;
    }

    public async Task<string> GetBranchHeadSha()
    {
        await GetConfiguration();

        var branch = await _gitHubClient.Repository.Branch.Get(
            _coreSettings.Owner,
            _coreSettings.RepoName,
            _coreSettings.Branch);

        return branch.Commit.Sha;
    }

    public async Task<RepositoryContent> GetFile(string filePath, string reference = null)
    {
        await GetConfiguration();

        var fileDetails = await _gitHubClient.Repository.Content.GetAllContentsByRef(
            _coreSettings.Owner,
            _coreSettings.RepoName,
            filePath, reference ?? _coreSettings.Branch);

        var firstContent = fileDetails.First();
        
   
        return firstContent;
    }
    
    public sealed class BranchMovedException : Exception
    {
        public BranchMovedException(string latestSha)
            : base("The selected data set changed before the publish could be completed.")
        {
            LatestSha = latestSha;
        }

        public string LatestSha { get; }
    }

    public async Task<string> UpdateFilesAtomically(IReadOnlyDictionary<string, string> files, string expectedHeadSha, string summary)
    {
        if (files.Count == 0)
        {
            return expectedHeadSha;
        }

        await GetConfiguration();
        var branch = await _gitHubClient.Repository.Branch.Get(_coreSettings.Owner, _coreSettings.RepoName, _coreSettings.Branch);
        if (!string.Equals(branch.Commit.Sha, expectedHeadSha, StringComparison.Ordinal))
        {
            throw new BranchMovedException(branch.Commit.Sha);
        }

        var parentCommit = await _gitHubClient.Git.Commit.Get(_coreSettings.Owner, _coreSettings.RepoName, expectedHeadSha);
        var newTree = new NewTree { BaseTree = parentCommit.Tree.Sha };
        foreach (var file in files)
        {
            newTree.Tree.Add(new NewTreeItem
            {
                Path = Path.Combine(_coreSettings.Folder, file.Key).Replace('\\', '/'),
                Mode = "100644",
                Type = TreeType.Blob,
                Content = file.Value
            });
        }

        var tree = await _gitHubClient.Git.Tree.Create(_coreSettings.Owner, _coreSettings.RepoName, newTree);

        var commit = await _gitHubClient.Git.Commit.Create(
            _coreSettings.Owner,
            _coreSettings.RepoName,
            new NewCommit(summary, tree.Sha, new[] { expectedHeadSha }));

        try
        {
            await _gitHubClient.Git.Reference.Update(
                _coreSettings.Owner,
                _coreSettings.RepoName,
                $"heads/{_coreSettings.Branch}",
                new ReferenceUpdate(commit.Sha, force: false));
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var latestBranch = await _gitHubClient.Repository.Branch.Get(_coreSettings.Owner, _coreSettings.RepoName, _coreSettings.Branch);
            if (!string.Equals(latestBranch.Commit.Sha, expectedHeadSha, StringComparison.Ordinal))
            {
                throw new BranchMovedException(latestBranch.Commit.Sha);
            }

            throw;
        }

        return commit.Sha;
    }

    public async Task<string> GetEditorDraft(string branch)
    {
        await GetConfiguration();
        var key = await _jsRuntime.InvokeAsync<string>("dataEditorDrafts.key", _coreSettings.Owner, _coreSettings.RepoName, branch);
        return await _jsRuntime.InvokeAsync<string>("dataEditorDrafts.get", key);
    }

    public async Task SaveEditorDraft(string branch, string draft)
    {
        await GetConfiguration();
        var key = await _jsRuntime.InvokeAsync<string>("dataEditorDrafts.key", _coreSettings.Owner, _coreSettings.RepoName, branch);
        await _jsRuntime.InvokeVoidAsync("dataEditorDrafts.set", key, draft);
    }

    public async Task ClearEditorDraft(string branch)
    {
        await GetConfiguration();
        var key = await _jsRuntime.InvokeAsync<string>("dataEditorDrafts.key", _coreSettings.Owner, _coreSettings.RepoName, branch);
        await _jsRuntime.InvokeVoidAsync("dataEditorDrafts.remove", key);
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
