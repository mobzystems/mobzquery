# MOBZQuery - Query a range of data sources

MOBZQuery tries to generalize executing queries against a range of data sources. Initially,
only OleDb data sources we supported, but since there are no generic OleDb providers for
SQLite and MySql, these two were added explicitly.

## Syntax

mobzquery --help shows:

```
Description:
  Query data sources using OleDB

Usage:
  mobzquery [command] [options]

Options:
  -?, -h, --help  Show help and usage information
  --version       Show version information

Commands:
  query <source> <sqlquery> <parameters>  Query a data source and display the results
```

The only supported command is 'query':

```
Description:
  Query a data source and display the results

Usage:
  mobzquery query <source> <sqlquery> [<parameters>...] [options]

Arguments:
  <source>      The data source. This is a file name when a type is specified, otherwise an OleDb connection string
  <sqlquery>    A SQL query
  <parameters>  Parameters for the SQL Query. Should appear in the SQL query as @1, @2, ....

Options:
  -i, --info                         Show column info
  -t, --type <ace|jet|mysql|sqlite>  The data source type. If provided, the source is simply a file name
  -v, --verbose                      Show detailed output
  -s, --scalar, --single             Expect and output only a single value
  -?, -h, --help                     Show help and usage information
```

## MOBZystems.Data, Spectre.Console and System.CommandLine

The actual query is executed via MOBZystems.Data, our lightweight data libraary wrapping DbConnection. The results
are displayed on the screen using Spectre.Console and the command structure of the application is provided by 
System.CommandLine.

## AOT - not

Initially, the project was intended to be published using AOT (Ahead Of Time compilation) which should have been
possible because both Spectre.Console and System.CommandLine support is. Unfortunately, System.Data.OleDb doesn't.
What's worse is that even application trimming won't work since the System.Data.MySql doesn't work with trimming!

A pure 'empty' AOT application would have had a size of about 10 MB. With System.Data.OleDb it would have been
appox. 15 MB iwht trimming, but wihtout either we're at 75 MB or more. It is what it is.

At least this way it is self-contained. A framework-dependent version of the application is still only 
