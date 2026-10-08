FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY src/Landing/Landing.csproj src/Landing/
RUN dotnet restore src/Landing/Landing.csproj
COPY src/Landing/ src/Landing/
RUN dotnet publish src/Landing/Landing.csproj --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Same as the base image's default, stated here because the health check and proxy rely on it.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
# Last, so a new commit SHA does not invalidate the layers above.
ARG APP_VERSION=unknown
ENV APP_VERSION=${APP_VERSION}
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --start-interval=2s --retries=3 \
    CMD ["dotnet", "Landing.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "Landing.dll"]
