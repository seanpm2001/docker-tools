// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.VisualStudio.Services.Common;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class AzdoOptions
    {
        public string AccessToken { get; set; } = string.Empty;

        public string Organization { get; set; } = string.Empty;

        public string Project { get; set; } = string.Empty;

        public string? AzdoRepo { get; set; }

        public string? AzdoBranch { get; set; }

        public string? AzdoPath { get; set; }

        public (Uri BaseUrl, VssCredentials Credentials) GetConnectionDetails() =>
            (new Uri($"https://dev.azure.com/{Organization}"), new VssBasicCredential(string.Empty, AccessToken));
    }

    public class AzdoOptionsBuilder : OptionsBuilder
    {
        private AzdoOptionsBuilder()
        {
        }

        public static AzdoOptionsBuilder Build() => new();

        public static AzdoOptionsBuilder BuildWithDefaults() =>
            Build()
                .WithAccessToken(isRequired: true)
                .WithOrganization(isRequired: true)
                .WithProject(isRequired: true)
                .WithRepo()
                .WithBranch()
                .WithPath();

        public AzdoOptionsBuilder WithRepo(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Azure DevOps repo") =>
            AddSymbol<AzdoOptionsBuilder, string>(
                "azdo-repo", nameof(AzdoOptions.AzdoRepo), isRequired, defaultValue, description);

        public AzdoOptionsBuilder WithBranch(
            bool isRequired = false,
            string? defaultValue = "main",
            string description = "Azure DevOps branch") =>
            AddSymbol<AzdoOptionsBuilder, string>(
                "azdo-branch", nameof(AzdoOptions.AzdoBranch), isRequired, defaultValue, description);

        public AzdoOptionsBuilder WithPath(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Azure DevOps path") =>
            AddSymbol<AzdoOptionsBuilder, string>(
                "azdo-path", nameof(AzdoOptions.AzdoPath), isRequired, defaultValue, description);

        public AzdoOptionsBuilder WithAccessToken(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Azure DevOps access token") =>
            AddSymbol<AzdoOptionsBuilder, string>(
                "azdo-pat", nameof(AzdoOptions.AccessToken), isRequired, defaultValue, description);

        public AzdoOptionsBuilder WithOrganization(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Azure DevOps organization") =>
            AddSymbol<AzdoOptionsBuilder, string>(
                "azdo-org", nameof(AzdoOptions.Organization), isRequired, defaultValue, description);

        public AzdoOptionsBuilder WithProject(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Azure DevOps project") =>
            AddSymbol<AzdoOptionsBuilder, string>(
                "azdo-project", nameof(AzdoOptions.Project), isRequired, defaultValue, description);
    }
}
#nullable disable
