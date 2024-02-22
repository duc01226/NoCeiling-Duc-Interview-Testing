#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Build csproj files layer
FROM alpine AS csproj
WORKDIR /src
COPY PlatformExampleApp/PlatformExampleApp.Shared/PlatformExampleApp.Shared.csproj PlatformExampleApp/PlatformExampleApp.Shared/
COPY PlatformExampleApp/PlatformExampleApp.Test/PlatformExampleApp.Test.csproj PlatformExampleApp/PlatformExampleApp.Test/
COPY PlatformExampleApp/PlatformExampleApp.TextSnippet.Api/PlatformExampleApp.TextSnippet.Api.csproj PlatformExampleApp/PlatformExampleApp.TextSnippet.Api/
COPY PlatformExampleApp/PlatformExampleApp.TextSnippet.Application/PlatformExampleApp.TextSnippet.Application.csproj PlatformExampleApp/PlatformExampleApp.TextSnippet.Application/
COPY PlatformExampleApp/PlatformExampleApp.TextSnippet.Domain/PlatformExampleApp.TextSnippet.Domain.csproj PlatformExampleApp/PlatformExampleApp.TextSnippet.Domain/
COPY PlatformExampleApp/PlatformExampleApp.TextSnippet.Infrastructure/PlatformExampleApp.TextSnippet.Infrastructure.csproj PlatformExampleApp/PlatformExampleApp.TextSnippet.Infrastructure/
COPY PlatformExampleApp/PlatformExampleApp.TextSnippet.Persistence/PlatformExampleApp.TextSnippet.Persistence.csproj PlatformExampleApp/PlatformExampleApp.TextSnippet.Persistence/
COPY PlatformExampleApp/PlatformExampleApp.TextSnippet.Persistence.Mongo/PlatformExampleApp.TextSnippet.Persistence.Mongo.csproj PlatformExampleApp/PlatformExampleApp.TextSnippet.Persistence.Mongo/
COPY PlatformExampleApp/PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo/PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo.csproj PlatformExampleApp/PlatformExampleApp.TextSnippet.Persistence.MultiDbDemo.Mongo/
COPY PlatformExampleApp/PlatformExampleApp.TextSnippet.Persistence.PostgreSql/PlatformExampleApp.TextSnippet.Persistence.PostgreSql.csproj PlatformExampleApp/PlatformExampleApp.TextSnippet.Persistence.PostgreSql/


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore Platform packages
COPY Platform/Easy.Platform/Easy.Platform.csproj Platform/Easy.Platform/
COPY Platform/Easy.Platform.AspNetCore/Easy.Platform.AspNetCore.csproj Platform/Easy.Platform.AspNetCore/
COPY Platform/Easy.Platform.EfCore/Easy.Platform.EfCore.csproj Platform/Easy.Platform.EfCore/
COPY Platform/Easy.Platform.FireBasePushNotification/Easy.Platform.FirebasePushNotification.csproj Platform/Easy.Platform.FireBasePushNotification/
COPY Platform/Easy.Platform.HangfireBackgroundJob/Easy.Platform.HangfireBackgroundJob.csproj Platform/Easy.Platform.HangfireBackgroundJob/
COPY Platform/Easy.Platform.MongoDB/Easy.Platform.MongoDB.csproj Platform/Easy.Platform.MongoDB/
COPY Platform/Easy.Platform.RabbitMQ/Easy.Platform.RabbitMQ.csproj Platform/Easy.Platform.RabbitMQ/
COPY Platform/Easy.Platform.RedisCache/Easy.Platform.RedisCache.csproj Platform/Easy.Platform.RedisCache/
COPY Platform/Easy.Platform.AzureFileStorage/Easy.Platform.AzureFileStorage.csproj Platform/Easy.Platform.AzureFileStorage/

RUN dotnet restore ./Platform/Easy.Platform/Easy.Platform.csproj
RUN dotnet restore ./Platform/Easy.Platform.AspNetCore/Easy.Platform.AspNetCore.csproj
RUN dotnet restore ./Platform/Easy.Platform.EfCore/Easy.Platform.EfCore.csproj
RUN dotnet restore ./Platform/Easy.Platform.FireBasePushNotification/Easy.Platform.FirebasePushNotification.csproj
RUN dotnet restore ./Platform/Easy.Platform.HangfireBackgroundJob/Easy.Platform.HangfireBackgroundJob.csproj
RUN dotnet restore ./Platform/Easy.Platform.MongoDB/Easy.Platform.MongoDB.csproj
RUN dotnet restore ./Platform/Easy.Platform.RabbitMQ/Easy.Platform.RabbitMQ.csproj
RUN dotnet restore ./Platform/Easy.Platform.RedisCache/Easy.Platform.RedisCache.csproj
RUN dotnet restore ./Platform/Easy.Platform.AzureFileStorage/Easy.Platform.AzureFileStorage.csproj

# Restore packages
COPY NuGetDocker.Config ./nuget.config
COPY --from=csproj /src .
RUN dotnet restore ./PlatformExampleApp/PlatformExampleApp.TextSnippet.Api/PlatformExampleApp.TextSnippet.Api.csproj

FROM build AS publish
ARG Configuration=Release

COPY ./.editorconfig ./.editorconfig
COPY Platform Platform
COPY PlatformExampleApp PlatformExampleApp
RUN dotnet publish ./PlatformExampleApp/PlatformExampleApp.TextSnippet.Api/PlatformExampleApp.TextSnippet.Api.csproj -c ${Configuration} -o /app --no-restore

FROM base AS final
# install curl to do health check
RUN apt-get update && apt-get install -y curl
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["/bin/sh", "-c" , "update-ca-certificates && dotnet PlatformExampleApp.TextSnippet.Api.dll"]
