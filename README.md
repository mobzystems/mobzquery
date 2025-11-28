# MOBZQuery - Query a range of data sources

MOBZQuery tries to generalize executing queries against a range of data sources. Initially, only OleDb data sources we supported, but since there are no generic OleDb providers for SQLite and MySql, these two were added explicitly.

## Syntax

Running mobzquery without any argument shows:

```
mobzquery v0.9.0 by MOBZystems - https://www.mobzystems.com (x86)

Description:
  Query data sources using OleDB

Usage:
  mobzquery [command] [options]

Options:
  -t, --type <ace|jet|mysql|sqlite>  The data source type. If provided, the source is simply a file name (except for MySql)
  -v, --verbose                      Show detailed output
  -?, -h, --help                     Show help and usage information
  --version                          Show version information

Commands:
  info <source> <sqlquery> <parameters>    Show column information about a query
  table <source> <sqlquery> <parameters>   Query a data source and show contents as a table
  json <source> <sqlquery> <parameters>    Query a data source and show contents as JSON
  csv <source> <sqlquery> <parameters>     Query a data source and show contents as CSV
  scalar <source> <sqlquery> <parameters>  Query a data source for a single value
  update                                   Check for updates
```

In other words, MOBZQuery supports the following commands:

- info
- table
- json
- csv
- scalar

(Plus 'update' which checks for updates)

All expect three arguments: the data source to query, the query to execute and optional parameters for the quuery.

### Data Source

By default, the data source is an OleDb connection string. But these can be cumbersome, and the OleDb provider for MySql and Sqlite are not
available as NuGet packages, so the data souce can have one of these additional types:

- ace (Office/Access format for .accdb files). The data source is the path of the database file
- jet (Office/Access format for .mdb file). The data source is the path of the database file
- mysql. The data source is the connection string for a MySqlConnection()
- sqlite. The data source is the path of the database file

So valid data sources are:

--type ace "\some\path\file.accdb"
--type jet "\some\path\file.mdb"
--type mysql "Server=127.0.0.1;Port=3306;Database=shoppinglist;Uid=root;Pwd=laurier46"
--type sqlite "\some\path\file.db"

Tip: by specifying the --verbose switch the connection string used is displayed. When you need to supply more information (i.e. user name or password for .mdb files) you can see what connection string to start from.

### SQL Query and Parameters

After the data source, a SQL query must be specified, e.g. "select * from Genres*". This query can contain parameters, consisting of @x where x is a number starting from 1, e.g. "select * from Genres where GenreId between @1 and @2". The full command  to display these rows from the Sqlite sample database would be:

    mobzquery table --type sqlite "d:\chinook.db" "select * from Genres where GenreId between @1 and @2" 5 10

Here, parameter @1 gets the value 5 and @2 the value 10, displaying all rows with and Id between 5 and 10:

```
┌─────────┬───────────────┐
│ GenreId │ Name          │
├─────────┼───────────────┤
│ 5       │ Rock And Roll │
│ 6       │ Blues         │
│ 7       │ Latin         │
│ 8       │ Reggae        │
│ 9       │ Pop           │
│ 10      │ Soundtrack    │
└─────────┴───────────────┘
```

### Info

The info command queries the data source (getting **all** records so be sure to limit them if needed!) and displays metadata. In the previous example:

    mobzquery info --type sqlite "d:\chinook.db" "select * from Genres where 0=1"

```
┌───────┬─────────┬────────┐
│ Index │ Name    │ Type   │
├───────┼─────────┼────────┤
│ 0     │ GenreId │ Int64  │
│ 1     │ Name    │ String │
└───────┴─────────┴────────┘
```

(The 0=1 filter makes sure we don't actually return any rows and we don't need parameters anymore)

### Table, CSV and JSON

All three get all rows in the specified query from the data source and display them on screen (or to a file if you redirect).

- 'table' displays a nice formatted table (thanks to Spectre.Console)
- 'csv' writes the data in Comma Separated Values format, surrounding strings with double quotes and separating values with commas. Dates are converted to string using an ISO-like format (yyyy-mm-dd HH:mm:ss)
- 'json' writes what looks like a JSON file. It's fake, but should be enough. String values are escaped partially: the double quotes, newline and carriage return are escaped by a backslash - and so are baskslashes

The JSON-like escaping mechanism can be applied to the CSV export as well, by specifying the --escape flag.

Here's the data in CSV format:

```
"GenreId","Name"
5,"Rock And Roll"
6,"Blues"
7,"Latin"
8,"Reggae"
9,"Pop"
10,"Soundtrack"
```

... and in JSON format (truncated):

```
[
  {
    "GenreId": 5,
    "Name": "Rock And Roll",
  },
  {
    "GenreId": 6,
    "Name": "Blues",
  },
  ...
  {
    "GenreId": 10,
    "Name": "Soundtrack",
  },
]
```

### Scalar

The 'scalar' command expects the query to return a single row with a single column. The value of that column is written to output. No column name, just the value.

## Verbose output

Specifying --verbose on the command line displays some extra information in green.

## MOBZystems.Data, Spectre.Console and System.CommandLine

The actual query is executed via MOBZystems.Data, our lightweight data library wrapping DbConnection. The results are displayed on the screen using Spectre.Console and the command structure of the application is provided by System.CommandLine.

## AOT - not

Initially, the project was intended to be published using AOT (Ahead Of Time compilation) which should have been possible because both Spectre.Console and System.CommandLine support is. Unfortunately, System.Data.OleDb doesn't. What's worse is that even application trimming won't work since the System.Data.MySql doesn't work with trimming! A pure 'empty' AOT application would have had a size of about 10 MB. With System.Data.OleDb it would have been appox. 15 MB iwht trimming, but wihtout either we're at 75 MB or more. It is what it is. At least this way it is self-contained. A framework-dependent version of the application is still only 
