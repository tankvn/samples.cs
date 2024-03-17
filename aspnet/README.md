

## Create a web app project

```bat
dotnet new webapp -o webapp
```

## Trust the development certificate

```bat
dotnet dev-certs https --trust
```

## Run the app

```
cd webapp
dotnet watch run
```