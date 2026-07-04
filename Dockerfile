FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

COPY . .
RUN dotnet restore
RUN dotnet build --configuration Release --no-restore

EXPOSE 5046
ENV ASPNETCORE_URLS=http://0.0.0.0:5046

# Starting commands:
CMD ["dotnet", "run", "--project", "cobblersBackend", "--no-build"]
