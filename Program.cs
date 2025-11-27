// See https://aka.ms/new-console-template for more information

using Google.Protobuf.WellKnownTypes;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.CommandLine;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

var rootCommand = new RootCommand("Query data sources using OleDB");
rootCommand.SetAction(_ => ShowHelp());

// Data source, SQL query and parameters (for all commands)
var sourceArgument = new Argument<string>("source") { Description = "The data source. This is a file name when a type is specified, otherwise an OleDb connection string" };
var sqlqueryArgument = new Argument<string>("sqlquery") { Description = "A SQL query" };
var sqlparametersArgument = new Argument<string[]>("parameters") { Description = "Parameters for the SQL Query. Should appear in the SQL query as @1, @2, ...." };

// Common options
var typeOption = new Option<string>("--type", "-t") { Description = "The data source type. If provided, the source is simply a file name (except for MySql)", Recursive = true };
typeOption.AcceptOnlyFromAmong("sqlite", "mysql", "ace", "jet");
var verboseOption = new Option<bool>("--verbose", "-v") { Description = "Show detailed output", Recursive = true };

// csv only:
var escapeOption = new Option<bool>("--escape", "-e") { Description = "Escape backslashes and \\r and \\n in strings" };

// Add default options for all commands
rootCommand.Add(typeOption);
rootCommand.Add(verboseOption);

var infoCommand = new Command("info", "Show column information about a query");
infoCommand.Add(sourceArgument);
infoCommand.Add(sqlqueryArgument);
infoCommand.Add(sqlparametersArgument);
infoCommand.SetAction(pr => QueryDataSource(pr, "info"));
rootCommand.Add((infoCommand));

var tableCommand = new Command("table", "Query a data source and show contents in a table");
tableCommand.Add(sourceArgument);
tableCommand.Add(sqlqueryArgument);
tableCommand.Add(sqlparametersArgument);
tableCommand.SetAction(pr => QueryDataSource(pr,"table"));
rootCommand.Add((tableCommand));

var csvCommand = new Command("csv", "Query a data source and show contents as CSV");
csvCommand.SetAction(pr => QueryDataSource(pr, "csv"));
csvCommand.Add(sourceArgument);
csvCommand.Add(sqlqueryArgument);
csvCommand.Add(sqlparametersArgument);
// Extra option for csv
csvCommand.Add(escapeOption);
rootCommand.Add((csvCommand));

var scalarCommand = new Command("scalar", "Query a data source for a single value");
// scalarCommand.Aliases = ["single"];
scalarCommand.Add(sourceArgument);
scalarCommand.Add(sqlqueryArgument);
scalarCommand.Add(sqlparametersArgument);
scalarCommand.SetAction(pr => QueryDataSource(pr, "scalar"));
rootCommand.Add((scalarCommand));

try
{
  var parseResult = rootCommand.Parse(args);
  if (parseResult == null)
    throw new Exception("Cannot parse arguments");
  parseResult.Invoke();
}
catch (Exception ex)
{
  AnsiConsole.MarkupInterpolated($"[red]{ex.Message}[/]");
}

int ShowHelp()
{
  var version = Assembly.GetEntryAssembly()?.GetName().Version;

  AnsiConsole.MarkupLineInterpolated($"mobzquery v[green]{version?.ToString(3) ?? "???"}[/] by [green]MOBZystems[/] - [link]https://www.mobzystems.com[/] ({(Environment.Is64BitProcess ? "x64" : "x86")})");
  AnsiConsole.WriteLine();
  rootCommand.Parse("--help").Invoke();
  return 0;
}

DbConnection TryCreateOleDbConnection(string source)
{
  {
    try
    {
      return new OleDbConnection(source);
    }
    catch (Exception ex)
    {
      throw new Exception($"Could not process the OleDd connection string. It should contain at least 'Provider='. Alternatively, specify --type and use a file name for the source.\r\nMessage was: {ex.Message}");
    }
  }
}

async Task<int> QueryDataSource(ParseResult pr, string command)
{
  try
  {
    // For all command: the data source arguments
    var source = pr.GetRequiredValue<string>("source");
    var query = pr.GetRequiredValue<string>("sqlquery");
    var parameters = pr.GetValue<string[]>("parameters");
    /// ... and the recursive options
    var type = pr.GetValue<string>("--type");
    var verbose = pr.GetValue<bool>("--verbose");

    // For csv only
    var escape = command == "csv" ? pr.GetRequiredValue<bool>("--escape") : false;

    // Create a DbConnection
    DbConnection connection = type switch
    {
      "jet" => new OleDbConnection($"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={source}"),
      "ace" => new OleDbConnection($"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={source};Persist Security Info=False"),
      "sqlite" => new SqliteConnection($"Data Source={source}"),
      "mysql" => new MySqlConnection(source),
      null => TryCreateOleDbConnection(source),
      _ => throw new Exception($"Unknown type '{type ?? "null"}'"),
    };

    if (verbose)
      AnsiConsole.MarkupLineInterpolated($"[green]Using connection string '{connection.ConnectionString}'[/]");

    using (var dataConnection = new MOBZystems.Data.DataConnection(connection, true))
    {
      if (verbose)
        AnsiConsole.MarkupLineInterpolated($"[green]Connection opened[/]");

      // Build a parameter-array
      var sqlParameters = parameters == null ? [] : parameters.Select((p, index) => ((index + 1).ToString(), p as object)).ToArray();

      // Query the data source
      var result = await dataConnection.SelectAsync(query, sqlParameters);
      // Get the row count
      var rowCount = result.Count();

      // Do the work for each command
      if (command == "info")
      {
        // info command
        Table columnTable = new Table();
        columnTable.AddColumns("Index", "Name", "Type");
        foreach (string columnName in result.ColumnNames)
        {
          var column = result.Column(columnName);
          columnTable.AddRow(column.Index.ToString(), Markup.Escape(column.Name), column.DataType.Name);
        }
        AnsiConsole.Write(columnTable);
      }
      else if (rowCount == 0)
      {
        // All other commands without any output
        if (verbose)
          AnsiConsole.MarkupLineInterpolated($"[green]Query returned 0 rows[/]");
      }
      else if (command == "scalar" )
      {
        // Just one value
        if (rowCount != 1)
          throw new Exception($"Query returned {rowCount} rows, expected 1");
        if (result.ColumnNames.Length != 1)
          throw new Exception($"Query returned {result.ColumnNames.Length} columns, expected 1");
        var value = result.Single()[0];
        if (value == null)
          AnsiConsole.MarkupLine("[red]<NULL>[/]");
        else
          AnsiConsole.MarkupLineInterpolated($"{value}");
      }
      else if (command == "table")
      {
        // Output a table
        Table dataTable = new Table();
        dataTable.Collapse();
        dataTable.AddColumns(result.ColumnNames.Select(n => Markup.Escape(n)).ToArray());
        var values = new IRenderable[result.ColumnNames.Length];

        foreach (var row in result)
        {
          for (int i = 0; i < result.ColumnNames.Length; i++)
            values[i] = row[i] != null ? new Markup(Markup.Escape(row[i].ToString()!)) : new Markup("[red]<NULL>[/]");
          dataTable.AddRow(values);
        }
        AnsiConsole.Write(dataTable);

        if (verbose)
        {
          var count = result.ToArray().Length;
          AnsiConsole.MarkupLineInterpolated($"[green]{count} row(s)[/]");
        }
      }
      else if (command == "csv")
      {
        // Output CSV
        Console.WriteLine(string.Join(",", result.ColumnNames.Select(name => $"\"{name}\"")));
        var values = new string[result.ColumnNames.Length];
        foreach (var row in result)
        {
          for (int i = 0; i < result.ColumnNames.Length; i++)
          {
            values[i] = (row[i] switch
            {
              null => "<NULL",
              DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss"),
              string s when escape => $"\"{s}\"" // Surround with quotes
                .Replace("\\", "\\\\") // Double backslashes
                .Replace("\r", "\\r")  // Replace \r
                .Replace("\n", "\\n"), // Replace \n
              string s when !escape => $"\"{s}\"", // Surround with quotes
              object o => o.ToString()
            })!;
          }
          Console.WriteLine(string.Join(",", values));
        }
      }
      else
      {
        throw new Exception($"Unknow opration: '{command}");
      }

      return 0;
    }
  }

  catch (Exception ex)
  {
    AnsiConsole.MarkupInterpolated($"[red]Error executing 'query': {ex.Message}[/]");
    return 1;
  }
}
