// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.VersionTools.Automation;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GitOptions : IGitHubFileRef
    {
        public string AuthToken { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        public GitHubAuth ToGitHubAuth()
        {
            return new GitHubAuth(AuthToken, Username, Email);
        }

        public Credentials ToOctokitCredentials() => new Credentials(AuthToken);

        public Uri GetRepoUrl() => new Uri($"https://github.com/{Owner}/{Repo}");
    }

    public class GitOptionsBuilder : OptionsBuilder
    {
        private GitOptionsBuilder()
        {
        }

        public static GitOptionsBuilder Build() => new();

        public static GitOptionsBuilder BuildWithDefaults() =>
            Build()
                .WithUsername(isRequired: true)
                .WithEmail(isRequired: true)
                .WithAuthToken(isRequired: true)
                .WithBranch()
                .WithOwner()
                .WithPath()
                .WithRepo();

        public GitOptionsBuilder WithUsername(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Username to use for GitHub connection") =>
            AddSymbol<GitOptionsBuilder, string>(
                "git-username", nameof(GitOptions.Username), isRequired, defaultValue, description);

        public GitOptionsBuilder WithEmail(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Email to use for GitHub connection") =>
            AddSymbol<GitOptionsBuilder, string>(
                "git-email", nameof(GitOptions.Email), isRequired, defaultValue, description);

        public GitOptionsBuilder WithAuthToken(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Auth token to use to connect to GitHub") =>
            AddSymbol<GitOptionsBuilder, string>(
                "git-token", nameof(GitOptions.AuthToken), isRequired, defaultValue, description);

        public GitOptionsBuilder WithBranch(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Name of GitHub branch to access") =>
            AddSymbol<GitOptionsBuilder, string>
                ("git-branch", nameof(GitOptions.Branch), isRequired, defaultValue, description);

        public GitOptionsBuilder WithOwner(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Owner of the GitHub repo to access") =>
            AddSymbol<GitOptionsBuilder, string>(
                "git-owner", nameof(GitOptions.Owner), isRequired, defaultValue, description);

        public GitOptionsBuilder WithPath(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Path of the GitHub repo to access") =>
            AddSymbol<GitOptionsBuilder, string>(
                "git-path", nameof(GitOptions.Path), isRequired, defaultValue, description);

        public GitOptionsBuilder WithRepo(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Name of the GitHub repo to access") =>
            AddSymbol<GitOptionsBuilder, string>(
                "git-repo", nameof(GitOptions.Repo), isRequired, defaultValue, description);
    }
}
#nullable disable
