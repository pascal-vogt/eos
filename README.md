# EOS Command Line utility

This is just a bunch of command line functionalities that are useful to me, and might be to others as well.

This is how I build it (you might have to select different options depending on your OS):
```
dotnet publish --framework net8.0 --output C:/data/repo/tools/eos --runtime win-x64 --self-contained true
```

## Config

To setup the initial config file (or update it if new properties get added later on):
```
eos config --init
```

## Vertec

Initialize the Vertec cache with:
```
eos vertec --init
```

This caches all work log entries, all bank holidays and the date at which you started working.

If you run it again, the work log entries won't get updated, you can however delete a selected few cache files to force those to be fetched again.

You probably want to update the work log entries of the current month daily, this can be done like so:
```
eos vertec --update-month 01.2021
```

You can list entries or aggregate them:
```
eos vertec --list --from 01.01.2021 --to 05.01.2021 --text SearchText
eos vertec --list --to 10.01.2021
eos vertec --aggregate --from 10.01.2021
```

The default aggregation key is the project name, but it can be changed using `eos.config`:
```
{
  "AggregationConfig": [
    {
      "Key": "Project,Phase,Description",
      "Match": "^.*(EOS |EOS-).*$",
      "Replacement": "EOS"
    },
    {
      "Key": "Project,Description",
      "Match": "^.*(HOLIDAYS).*$",
      "Replacement": "HOLIDAYS"
    },
    {
      "Key": "Project",
      "Match": "^.*$",
      "Replacement": "$0"
    }
  ]
}
```

You can view your overtime:
```
eos vertec --overtime
eos vertec --overtime-at 01.01.2021
```

You can check if the presence matches the work logs:
```
eos vertec --check-presence --from 01.01.2020
```

## DB

Primarily meant to export realistic test data from a real database.

You will have to run the command at least twice. On the first run a
profile will be generated that you will have to edit. Basically you
need to specify which table is the entry point, which other tables
you want to export as well and what direction the foreign keys have
to be followed in.
```
eos db -c "Server=.; Database=someDb;Trusted_Connection=True;" --export-to data.sql --profile profile.json --id 02cd75a5-bb2c-4211-b744-288f3a552529
```

Potential ideas for future improvements include

- Inferring trivial exploration directions for foreign keys (would work for some but not all)
- Adding support for identity insert
- Adding support for other databases than MSSQL