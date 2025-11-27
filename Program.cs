// See https://aka.ms/new-console-template for more information

using Spectre.Console;
using System.CommandLine;
using System.Data.OleDb;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.Data;
using Spectre.Console.Rendering;
using System.Reflection.Metadata.Ecma335;
using System.Reflection;
using MySql.Data.MySqlClient;

var rootCommand = new RootCommand("Query data sources using OleDB");
rootCommand.SetAction(_ => ShowHelp());

var sourceArgument = new Argument<string>("source") { Description = "The data source. This is a file name when a type is specified, otherwise an OleDb connection string" };
var sqlqueryArgument = new Argument<string>("sqlquery") { Description = "A SQL query" };
var sqlparametersArgument = new Argument<string[]>("parameters") { Description = "Parameters for the SQL Query. Should appear in the SQL query as @1, @2, ...." };

var infoOption = new Option<bool>("--info", "-i") { Description = "Show column info" };
var typeOption = new Option<string>("--type", "-t") { Description = "The data source type. If provided, the source is simply a file name" };
typeOption.AcceptOnlyFromAmong("sqlite", "mysql", "ace", "jet");
var verboseOption = new Option<bool>("--verbose", "-v") { Description = "Show detailed output" };
var scalarOption = new Option<bool>("--scalar", "-s", "--single") { Description = "Expect and output only a single value" };

var queryCommand = new Command("query", "Query a data source and display the results");
queryCommand.SetAction(pr => QueryDataSource(pr));
queryCommand.Add(sourceArgument);
queryCommand.Add(sqlqueryArgument);
queryCommand.Add(sqlparametersArgument);
queryCommand.Add(infoOption);
queryCommand.Add(typeOption);
queryCommand.Add(verboseOption);
queryCommand.Add(scalarOption);

rootCommand.Add((queryCommand));

try
{
  var parseResult = rootCommand.Parse(args);
  if (parseResult == null)
    throw new Exception("Cannot parse arguments");
  parseResult.Invoke();
}
catch (Exception ex)
{
  // Console.Error.WriteLine($"{ex.Message}");
  AnsiConsole.MarkupInterpolated($"[red]{ex.Message}[/]");
}

int ShowHelp()
{
  var version = Assembly.GetEntryAssembly()?.GetName().Version;

  AnsiConsole.MarkupLineInterpolated($"mobzquery v[green]{version?.ToString(3) ?? "???"}[/] by [green]MOBZystems[/] - [link]https://www.mobzystems.com[/] ({(Environment.Is64BitProcess ? "x64" : "x86")})");
  AnsiConsole.WriteLine();
  // AnsiConsole.MarkupLineInterpolated($"Use [blue]mobzquery --help[/] for help.");
  // Console.WriteLine(rootCommand.Invoke());
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

async Task<int> QueryDataSource(ParseResult pr)
{
  try
  {
    var source = pr.GetRequiredValue<string>("source");
    var query = pr.GetRequiredValue<string>("sqlquery");
    var parameters = pr.GetValue<string[]>("parameters");
    var info = pr.GetRequiredValue<bool>("--info");
    var type = pr.GetValue<string>("--type");
    var verbose = pr.GetValue<bool>("--verbose");
    var scalar = pr.GetValue<bool>("--scalar");

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

      var sqlParameters = parameters == null ? [] : parameters.Select((p, index) => ((index + 1).ToString(), p as object)).ToArray();

      var result = await dataConnection.SelectAsync(query, sqlParameters);
      var rowCount = result.Count();
      if (info)
      {
        Table columnTable = new Table();
        columnTable.AddColumns("Index", "Name", "Type");
        foreach (string columnName in result.ColumnNames)
        {
          var column = result.Column(columnName);
          columnTable.AddRow(column.Index.ToString(), column.Name, column.DataType.Name);
        }
        AnsiConsole.Write(columnTable);
      }
      else if (rowCount == 0)
      {
        if (verbose)
          AnsiConsole.MarkupLineInterpolated($"[green]Query returned 0 rows[/]");
      }
      else if (scalar)
      {
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
      else
      {
        Table dataTable = new Table();
        dataTable.Collapse();
        dataTable.AddColumns(result.ColumnNames);
        var values = new IRenderable[result.ColumnNames.Length];

        foreach (var row in result)
        {
          for (int i = 0; i < result.ColumnNames.Length; i++)
          {
            values[i] = row[i] != null ? new Markup(Markup.Escape(row[i].ToString()!)) : new Markup("[red]<NULL>[/]");
          }
          dataTable.AddRow(values);
        }
        AnsiConsole.Write(dataTable);

        if (verbose)
        {
          var count = result.ToArray().Length;
          AnsiConsole.MarkupLineInterpolated($"[green]{count} row(s)[/]");
        }
      }
    }

    return 0;
  }

  catch (Exception ex)
  {
    AnsiConsole.MarkupInterpolated($"[red]Error executing 'query': {ex.Message}[/]");
    return 1;
  }
}