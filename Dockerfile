FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY src/Landing/Landing.csproj src/Landing/
RUN dotnet restore src/Landing/Landing.csproj
COPY src/Landing/ src/Landing/
RUN dotnet publish src/Landing/Landing.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
WORKDIR /app
ARG APP_VERSION=unknown
ENV APP_VERSION=${APP_VERSION}
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD ["dotnet", "Landing.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "Landing.dll"]
