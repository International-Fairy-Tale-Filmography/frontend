// File: DataEditor.Core/Services/GitService.cs
using System.Net;
using Octokit;
using Microsoft.JSInterop;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
    public string Login { get; set; }
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
        var githubAccessToken = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "github_accesstoken");
        if (string.IsNullOrWhiteSpace(githubAccessToken))
        {
            githubAccessToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "github_accesstoken");
            if (!string.IsNullOrWhiteSpace(githubAccessToken))
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "github_accesstoken", githubAccessToken);
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "github_accesstoken");
            }
        }

        var selectedBranch = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "data_editor_branch");
        if (string.IsNullOrWhiteSpace(selectedBranch))
        {
            selectedBranch = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "data_editor_branch");
            if (!string.IsNullOrWhiteSpace(selectedBranch))
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "data_editor_branch", selectedBranch);
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "data_editor_branch");
            }
        }

        _coreSettings.AccessToken = githubAccessToken;
        if (!string.IsNullOrWhiteSpace(selectedBranch))
        {
            _coreSettings.Branch = selectedBranch;
        }

        _gitHubClient.Credentials = string.IsNullOrWhiteSpace(_coreSettings.AccessToken)
            ? Credentials.Anonymous
            : new Credentials(_coreSettings.AccessToken);

        return _coreSettings;
    }

    public async Task SaveConfiguration(CoreSettingsModel settings)
    {

      
        _coreSettings.AccessToken = settings.AccessToken;
        _coreSettings.Owner = settings.Owner;
        _coreSettings.RepoName = settings.RepoName;
        _coreSettings.Branch = settings.Branch;
        _coreSettings.Folder = settings.Folder;

        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "github_accesstoken", _coreSettings.AccessToken);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "github_accesstoken");
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "data_editor_branch", _coreSettings.Branch);

        _gitHubClient.Credentials = new Credentials(_coreSettings.AccessToken);
    }

    public async Task ClearAccessToken()
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "github_accesstoken");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "github_accesstoken");
        _coreSettings.AccessToken = null;
        _gitHubClient.Credentials = Credentials.Anonymous;
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

    public async Task<List<string>> GetBranches()
    {
        await GetConfiguration();

        var branches = await _gitHubClient.Repository.Branch.GetAll(_coreSettings.Owner, _coreSettings.RepoName);
        return branches
            .Where(b => !b.Name.StartsWith("gh-pages-"))
            .Select(b => b.Name)
            .ToList();
    }

    public async Task<ValidationResult> ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ValidationResult { IsValid = false, ErrorMessage = "Enter a personal access token to connect." };
        }

        try
        {
            var testClient = new GitHubClient(new ProductHeaderValue("IFTF"))
            {
                Credentials = new Credentials(token)
            };

            var user = await testClient.User.Current();
            await testClient.Repository.Branch.Get(
                _coreSettings.Owner,
                _coreSettings.RepoName,
                _coreSettings.Branch);

            await testClient.Repository.Content.GetAllContentsByRef(
                _coreSettings.Owner,
                _coreSettings.RepoName,
                Path.Combine(_coreSettings.Folder, "Films.csv").Replace('\\', '/'),
                _coreSettings.Branch);

            return new ValidationResult { IsValid = true, Login = user.Login };
        }
        catch (AuthorizationException)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "GitHub rejected this token. Check that it is active and has access to the data repository."
            };
        }
        catch (NotFoundException)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "The token cannot access the configured data repository or selected data set. Check the repository selection and permissions."
            };
        }
        catch (ApiException ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"GitHub could not verify access to the data repository: {ex.Message}"
            };
        }
        catch (HttpRequestException ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Could not reach GitHub to verify the token: {ex.Message}"
            };
        }
    }
}
