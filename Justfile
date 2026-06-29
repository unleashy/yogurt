set windows-shell := ["pwsh.exe", "-c"]

default:
  just --list

build:
  dotnet build Yogurt.Server

run *args:
  dotnet run --project Yogurt.Server -- {{args}}

publish rid:
  dotnet publish -p:PublishProfile=Release -r {{rid}} Yogurt.Server
