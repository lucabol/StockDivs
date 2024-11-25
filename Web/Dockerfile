# Modified from https://www.thinktecture.com/en/net/native-aot-with-asp-net-core-overview/
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine3.20 AS build
RUN apk update && apk upgrade
RUN apk add --no-cache clang build-base zlib-dev
WORKDIR /code
COPY ./Web.csproj .
ARG RUNTIME_ID=linux-musl-x64
RUN dotnet restore -r $RUNTIME_ID
COPY . .
RUN dotnet publish \
    -c Release \
    -r $RUNTIME_ID \
    -o /app \
    --no-restore


FROM alpine:3.20 AS final
WORKDIR /app

COPY --from=build /app/Web ./Web
COPY --from=build /code/favicon.ico ./wwwroot/favicon.ico

run apk --no-cache add curl
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
    CMD curl --silent --fail http://localhost:80/health || exit 1

# Who knows if this is needed?
ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80

ENTRYPOINT ["./Web"]