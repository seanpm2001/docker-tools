// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GenerateDockerfilesCommandTests
    {
        private const string DockerfilePath = "1.0/sdk/os/Dockerfile";
        private const string DefaultDockerfile = "FROM Base";
        private const string DockerfileTemplatePath = "Dockerfile.Template";
        private const string DefaultDockerfileTemplate =
@"FROM Repo:2.1-{{OS_VERSION_BASE}}
ENV TEST1 {{if OS_VERSION = ""buster-slim"":IfWorks}}
ENV TEST2 {{VARIABLES[""Variable1""]}}";
        private const string ExpectedDockerfile =
@"FROM Repo:2.1-buster
ENV TEST1 IfWorks
ENV TEST2 Value1";

        private readonly Exception _exitException = new Exception();
        Mock<IEnvironmentService> _environmentServiceMock;

        public GenerateDockerfilesCommandTests()
        {
        }

        [Fact]
        public async Task GenerateDockerfilesCommand_Canonical()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext);

            await command.ExecuteAsync();

            string generatedDockerfile = File.ReadAllText(Path.Combine(tempFolderContext.Path, DockerfilePath));
            Assert.Equal(ExpectedDockerfile.NormalizeLineEndings(generatedDockerfile), generatedDockerfile);
        }

        [Fact]
        public async Task GenerateDockerfilesCommand_InvalidTemplate()
        {
            string template = "FROM $REPO:2.1-{{if:}}";
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext, template);

            Exception actualException = await Assert.ThrowsAsync<Exception>(command.ExecuteAsync);

            Assert.Same(_exitException, actualException);
            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task GenerateDockerfilesCommand_Validate_UpToDate()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext, dockerfile:ExpectedDockerfile, validate: true);

            await command.ExecuteAsync();

            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GenerateDockerfilesCommand_Validate_OutOfSync()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext, validate: true);

            Exception actualException = await Assert.ThrowsAsync<Exception>(command.ExecuteAsync);

            Assert.Same(_exitException, actualException);
            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Once);
            // Validate Dockerfile remains unmodified
            Assert.Equal(DefaultDockerfile, File.ReadAllText(Path.Combine(tempFolderContext.Path, DockerfilePath)));
        }

        [Theory]
        [InlineData("repo1:tag1", "ARCH_SHORT", "arm")]
        [InlineData("repo1:tag1", "ARCH_NUPKG", "arm32")]
        [InlineData("repo1:tag1", "ARCH_VERSIONED", "arm32v7")]
        [InlineData("repo1:tag2", "ARCH_VERSIONED", "amd64")]
        [InlineData("repo1:tag1", "ARCH_TAG_SUFFIX", "-arm32v7")]
        [InlineData("repo1:tag2", "ARCH_TAG_SUFFIX", "")]
        [InlineData("repo1:tag1", "OS_VERSION", "buster-slim")]
        [InlineData("repo1:tag1", "OS_VERSION_BASE", "buster")]
        [InlineData("repo1:tag1", "OS_VERSION_NUMBER", "")]
        [InlineData("repo1:tag3", "OS_VERSION_NUMBER", "3.12")]
        [InlineData("repo1:tag1", "OS_ARCH_HYPHENATED", "Debian-10-arm32")]
        [InlineData("repo1:tag2", "OS_ARCH_HYPHENATED", "NanoServer-1903")]
        [InlineData("repo1:tag3", "OS_ARCH_HYPHENATED", "Alpine-3.12")]
        [InlineData("repo1:tag1", "Variable1", "Value1", true)]
        public void GenerateDockerfilesCommand_SupportedSymbols(string tag, string symbol, string expectedValue, bool isVariable = false)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext);

            IReadOnlyDictionary<Value, Value> symbols = command.GetSymbols(command.Manifest.GetPlatformByTag(tag));

            Value variableValue;
            if (isVariable)
            {
                variableValue = symbols["VARIABLES"].Fields[symbol];
            }
            else
            {
                variableValue = symbols[symbol];
            }

            Assert.Equal(expectedValue, variableValue);
        }

        private GenerateDockerfilesCommand InitializeCommand(
            TempFolderContext tempFolderContext,
            string dockerfileTemplate = DefaultDockerfileTemplate,
            string dockerfile = DefaultDockerfile,
            bool validate = false)
        {
            DockerfileHelper.CreateFile(DockerfileTemplatePath, tempFolderContext, dockerfileTemplate);
            DockerfileHelper.CreateFile(DockerfilePath, tempFolderContext, dockerfile);

            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        CreatePlatform(
                            DockerfilePath,
                            new string[] { "tag1" },
                            OS.Linux,
                            "buster-slim",
                            Architecture.ARM,
                            "v7",
                            dockerfileTemplatePath: DockerfileTemplatePath),
                        CreatePlatform(
                            DockerfilePath,
                            new string[] { "tag2" },
                            OS.Windows,
                            "nanoserver-1903"),
                        CreatePlatform(
                            DockerfilePath,
                            new string[] { "tag3" },
                            OS.Linux,
                            "alpine3.12")))
            );
            AddVariable(manifest, "Variable1", "Value1");

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            _environmentServiceMock = new Mock<IEnvironmentService>();
            _environmentServiceMock
                .Setup(o => o.Exit(1))
                .Throws(_exitException);

            GenerateDockerfilesCommand command = new GenerateDockerfilesCommand(_environmentServiceMock.Object);
            command.Options.Manifest = manifestPath;
            command.Options.Validate = validate;
            command.LoadManifest();

            return command;
        }
    }
}