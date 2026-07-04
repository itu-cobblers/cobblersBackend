FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

# Copy source and restore/build dependencies at image build time.
COPY . .
RUN dotnet restore
RUN dotnet build --configuration Release --no-restore

EXPOSE 5046
ENV ASPNETCORE_URLS=http://0.0.0.0:5046

# Match the repository's normal start command from README.
CMD ["dotnet", "run", "--project", "cobblersBackend", "--no-build"]
