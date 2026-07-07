FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as distinct layers for better caching
COPY cobblersBackend/cobblersBackend.csproj cobblersBackend/
RUN dotnet restore cobblersBackend/cobblersBackend.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish cobblersBackend/cobblersBackend.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5046
ENV ASPNETCORE_URLS=http://0.0.0.0:5046
ENTRYPOINT ["dotnet", "cobblersBackend.dll"]
