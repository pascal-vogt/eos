# EOS Command Line utility

This is just a bunch of command line functionalities that are useful to me, and might be to others as well.

This is how I build it (you might have to select different options depending on your OS):
```
dotnet publish --framework netcoreapp3.1 --output C:/data/tools/eos --runtime win-x64 --self-contained true
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

You can view your overtime:
```
eos vertec --overtime
eos vertec --overtime-at 01.01.2021
```