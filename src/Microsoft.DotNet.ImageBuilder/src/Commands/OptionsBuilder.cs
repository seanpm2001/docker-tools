// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands;

#nullable enable
public abstract class OptionsBuilder
{
    private readonly List<Option> _options = new();
    private readonly List<Argument> _arguments = new();

    public IEnumerable<Option> GetCliOptions() => _options;

    public IEnumerable<Argument> GetCliArguments() => _arguments;

    protected TBuilder AddSymbol<TBuilder, TSymbol>(string alias, string propertyName, bool isRequired, TSymbol? defaultValue, string description)
        where TBuilder : OptionsBuilder
    {
        if (isRequired)
        {
            _arguments.Add(new Argument<TSymbol>(propertyName, description));
        }
        else
        {
            _options.Add(CreateOption(alias, propertyName, description, defaultValue is null ? default! : defaultValue));
        }

        return (TBuilder)this;
    }
}
#nullable disable
