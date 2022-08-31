// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PublishImageInfoCommand : ManifestCommand<PublishImageInfoOptions, PublishImageInfoOptionsBuilder>
    {
        private readonly IGitService _gitService;
        private readonly ILoggerService _loggerService;
        private const string CommitMessage = "Merging Docker image info updates from build";

        [ImportingConstructor]
        public PublishImageInfoCommand(IGitService gitService, ILoggerService loggerService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        protected override string Description => "Publishes a build's merged image info.";

        public override Task ExecuteAsync()
        {
            if (!string.IsNullOrEmpty(Options.GitOptions.Path) &&
                !string.IsNullOrEmpty(Options.AzdoOptions.AzdoPath) &&
                Options.AzdoOptions.AzdoPath != Options.GitOptions.Path)
            {
                throw new InvalidOperationException(
                    $"The file path for GitHub '{Options.GitOptions.Path}' must be equal to the file path for AzDO '{Options.AzdoOptions.AzdoPath}'.");
            }

            _loggerService.WriteHeading("PUBLISHING IMAGE INFO");

            string repoPath = Path.Combine(Path.GetTempPath(), "imagebuilder-repos", Options.GitOptions.Repo);
            if (Directory.Exists(repoPath))
            {
                FileHelper.ForceDeleteDirectory(repoPath);
            }

            try
            {
                GitRepoProvider gitRepoProvider = GitRepoProvider.Create(_gitService, Options);

                _loggerService.WriteSubheading("Cloning GitHub repo");
                using IRepository repo = gitRepoProvider.CloneRepository(repoPath);

                Uri imageInfoPathIdentifier = gitRepoProvider.GetFileUrl();

                _loggerService.WriteSubheading("Calculating new image info content");
                string? imageInfoContent = GetUpdatedImageInfo(repoPath);

                if (imageInfoContent is null)
                {
                    _loggerService.WriteMessage($"No changes to the '{imageInfoPathIdentifier}' file were needed.");
                    return Task.CompletedTask;
                }

                _loggerService.WriteMessage(
                    $"The '{imageInfoPathIdentifier}' file has been updated with the following content:" +
                        Environment.NewLine + imageInfoContent + Environment.NewLine);

                if (!Options.IsDryRun)
                {
                    UpdateGitRepos(imageInfoContent, repoPath, repo, gitRepoProvider);
                }
            }
            finally
            {
                if (Directory.Exists(repoPath))
                {
                    FileHelper.ForceDeleteDirectory(repoPath);
                }
            }

            return Task.CompletedTask;
        }

        private void UpdateGitRepos(string imageInfoContent, string repoPath, IRepository repo, GitRepoProvider gitRepoProvider)
        {
            string fullImageInfoPath = Path.Combine(repoPath, gitRepoProvider.ImageInfoPath);
            File.WriteAllText(fullImageInfoPath, imageInfoContent);
            _gitService.Stage(repo, fullImageInfoPath);

            Signature sig = new(Options.GitOptions.Username, Options.GitOptions.Email, DateTimeOffset.Now);
            Commit commit = repo.Commit(CommitMessage, sig, sig);

            _loggerService.WriteSubheading("Pushing changes");
            Uri commitUrl = gitRepoProvider.PushChanges(repo, commit.Sha);

            _loggerService.WriteMessage($"The '{gitRepoProvider.ImageInfoPath}' file was updated: {commitUrl}");
        }

        private string? GetUpdatedImageInfo(string repoPath)
        {
            ImageArtifactDetails srcImageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

            string repoImageInfoPath = Path.Combine(repoPath, Options.GitOptions.Path);
            string originalTargetImageInfoContents = File.ReadAllText(repoImageInfoPath);

            ImageArtifactDetails newImageArtifactDetails;

            if (originalTargetImageInfoContents != null)
            {
                ImageArtifactDetails targetImageArtifactDetails = ImageInfoHelper.LoadFromContent(
                    originalTargetImageInfoContents, Manifest, skipManifestValidation: true);

                RemoveOutOfDateContent(targetImageArtifactDetails);

                ImageInfoMergeOptions options = new ImageInfoMergeOptions
                {
                    IsPublish = true
                };

                ImageInfoHelper.MergeImageArtifactDetails(srcImageArtifactDetails, targetImageArtifactDetails, options);

                newImageArtifactDetails = targetImageArtifactDetails;
            }
            else
            {
                // If there is no existing file to update, there's nothing to merge with so the source data
                // becomes the target data.
                newImageArtifactDetails = srcImageArtifactDetails;
            }

            string newTargetImageInfoContents =
                JsonHelper.SerializeObject(newImageArtifactDetails) + Environment.NewLine;

            if (originalTargetImageInfoContents != newTargetImageInfoContents)
            {
                return newTargetImageInfoContents;
            }
            else
            {
                return null;
            }
        }

        private void RemoveOutOfDateContent(ImageArtifactDetails imageArtifactDetails)
        {
            for (int repoIndex = imageArtifactDetails.Repos.Count - 1; repoIndex >= 0; repoIndex--)
            {
                RepoData repoData = imageArtifactDetails.Repos[repoIndex];

                // Since the registry name is not represented in the image info, make sure to compare the repo name with the
                // manifest's repo model name which isn't registry-qualified.
                RepoInfo? manifestRepo = Manifest.AllRepos.FirstOrDefault(manifestRepo => manifestRepo.Name == repoData.Repo);

                // If there doesn't exist a matching repo in the manifest, remove it from the image info
                if (manifestRepo is null)
                {
                    imageArtifactDetails.Repos.Remove(repoData);
                    continue;
                }

                for (int imageIndex = repoData.Images.Count - 1; imageIndex >= 0; imageIndex--)
                {
                    ImageData imageData = repoData.Images[imageIndex];
                    ImageInfo manifestImage = imageData.ManifestImage;

                    // If there doesn't exist a matching image in the manifest, remove it from the image info
                    if (manifestImage is null)
                    {
                        repoData.Images.Remove(imageData);
                        continue;
                    }

                    for (int platformIndex = imageData.Platforms.Count - 1; platformIndex >= 0; platformIndex--)
                    {
                        PlatformData platformData = imageData.Platforms[platformIndex];
                        PlatformInfo? manifestPlatform = manifestImage.AllPlatforms
                            .FirstOrDefault(manifestPlatform => platformData.PlatformInfo == manifestPlatform);

                        // If there doesn't exist a matching platform in the manifest, remove it from the image info
                        if (manifestPlatform is null)
                        {
                            imageData.Platforms.Remove(platformData);
                        }
                    }
                }
            }

            if (imageArtifactDetails.Repos.Count == 0)
            {
                // Failsafe to prevent wiping out the image info due to a bug in the logic
                throw new InvalidOperationException(
                    "Removal of out-of-date content resulted in there being no content remaining in the target image info file. Something is probably wrong with the logic.");
            }
        }

        private abstract class GitRepoProvider
        {
            public static GitRepoProvider Create(IGitService gitService, PublishImageInfoOptions options)
            {
                if (!string.IsNullOrEmpty(options.GitOptions.Path))
                {
                    return new GitHubRepoProvider(gitService, options);
                }
                else if (!string.IsNullOrEmpty(options.AzdoOptions.AzdoPath))
                {
                    return new AzdoRepoProvider(gitService, options);
                }

                throw new InvalidDataException("Either GitHub or AzDO options must be specified.");
            }

            public abstract string ImageInfoPath { get; }

            protected abstract string Branch { get; }

            protected abstract string AccessToken { get; }

            public abstract IRepository CloneRepository(string repoPath);

            public abstract Uri GetFileUrl();

            public abstract Uri GetCommitUrl(string commitSha);

            public Uri PushChanges(IRepository repo, string commitSha)
            {
                Branch branch = repo.Branches[Branch];

                repo.Network.Push(branch,
                    new PushOptions
                    {
                        CredentialsProvider = (url, user, cred) => new UsernamePasswordCredentials
                        {
                            Username = AccessToken,
                            Password = string.Empty
                        }
                    });

                Uri gitHubCommitUrl = GetCommitUrl(commitSha);
                return gitHubCommitUrl;
            }
        }

        private class GitHubRepoProvider : GitRepoProvider
        {
            private readonly IGitService _gitService;
            private readonly PublishImageInfoOptions _options;

            public GitHubRepoProvider(IGitService gitService, PublishImageInfoOptions options)
            {
                _gitService = gitService;
                _options = options;
            }

            public override string ImageInfoPath => _options.GitOptions.Path;

            protected override string Branch => _options.GitOptions.Branch;

            protected override string AccessToken => _options.GitOptions.AuthToken;

            public override IRepository CloneRepository(string repoPath) =>
                _gitService.CloneRepository(
                    $"https://github.com/{_options.GitOptions.Owner}/{_options.GitOptions.Repo}",
                    repoPath,
                    new CloneOptions
                    {
                        BranchName = _options.GitOptions.Branch
                    });

            public override Uri GetFileUrl() => GitHelper.GetBlobUrl(_options.GitOptions);

            public override Uri GetCommitUrl(string commitSha) => GitHelper.GetCommitUrl(_options.GitOptions, commitSha);
        }

        private class AzdoRepoProvider : GitRepoProvider
        {
            private readonly IGitService _gitService;
            private readonly PublishImageInfoOptions _options;

            public AzdoRepoProvider(IGitService gitService, PublishImageInfoOptions options)
            {
                _gitService = gitService;
                _options = options;
            }

            public override string ImageInfoPath => _options.AzdoOptions.AzdoPath!;

            protected override string Branch => _options.AzdoOptions.AzdoBranch!;

            protected override string AccessToken => _options.AzdoOptions.AccessToken;

            public override IRepository CloneRepository(string repoPath) =>
                _gitService.CloneRepository(
                    $"https://dev.azure.com/{_options.AzdoOptions.Organization}/{_options.AzdoOptions.Project}/_git/{_options.AzdoOptions.AzdoRepo}",
                    repoPath,
                    new CloneOptions
                    {
                        BranchName = _options.AzdoOptions.AzdoBranch,
                        CredentialsProvider = (url, user, cred) => new UsernamePasswordCredentials
                        {
                            Username = _options.AzdoOptions.AccessToken,
                            Password = string.Empty
                        }
                    });

            public override Uri GetFileUrl() => GitHelper.GetFileUrl(_options.AzdoOptions);

            public override Uri GetCommitUrl(string commitSha) => GitHelper.GetCommitUrl(_options.AzdoOptions, commitSha);
        }
    }
}
#nullable disable
