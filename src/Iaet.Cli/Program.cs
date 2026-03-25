using System.CommandLine;
using Iaet.Cli.Commands;

var rootCommand = new RootCommand("IAET - Internal API Extraction Toolkit")
{
    CaptureCommand.Create(),
    CatalogCommand.Create()
};

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
