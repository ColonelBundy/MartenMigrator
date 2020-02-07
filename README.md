[![Nuget Package](https://badgen.net/nuget/v/Marten.Migrator)](https://www.nuget.org/packages/Marten.Migrator/)

## A work in progress.
> The goal of this project is to migrate data when a class has changed, changing something from object to string? you probably don't want to keep that data around since you'll get exceptions quering it so this library will first check if a conversion is possible, if not we'll discard the data. Otherwize it will leave the data as is and it'll get converted when you query it.
> Since this is a work in progress, only valuetypes are supported for now and there might be many unforseen circiumstances that may or may not work as expected.
