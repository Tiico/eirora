using Eirora.Balance;

// Usage:
//   dotnet run -c Release --project tools/Eirora.Balance                       (economy v1's week and 20 generated maps for four weeks)
//   dotnet run -c Release --project tools/Eirora.Balance -- --suite economy-v1 (only the runs of economy v1, section 7)
//   dotnet run -c Release --project tools/Eirora.Balance -- --suite generated --from 0 --seeds 20 --days 28
//   dotnet run -c Release --project tools/Eirora.Balance -- --maps generated --from 100 --seeds 5 --strategies near,sea,naive --profiles normal,low --days 56
// Writes balance.tsv and balance.md to --out (artifacts/balance by default) and the Markdown to standard output.
CommandLine command;
try
{
    command = CommandLine.Parse(args);
}
catch (ArgumentException e)
{
    Console.Error.WriteLine(e.Message);
    Console.Error.WriteLine(CommandLine.Usage);
    return 2;
}

var runs = command.Runs.Select(BalanceRun.Play).ToList();
Directory.CreateDirectory(command.Output);
File.WriteAllText(Path.Combine(command.Output, "balance.tsv"), Report.Tsv(runs));
string markdown = Report.Markdown(runs);
File.WriteAllText(Path.Combine(command.Output, "balance.md"), markdown);
Console.Write(markdown);
Console.Error.WriteLine($"Wrote {runs.Count} runs to {Path.Combine(command.Output, "balance.tsv")} and balance.md.");
return 0;
