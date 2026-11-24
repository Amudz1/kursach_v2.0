FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props .
COPY src ./src
RUN dotnet publish src/AiAssistant.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/attachments && chown $APP_UID /app/attachments
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AiAssistant.Api.dll"]
