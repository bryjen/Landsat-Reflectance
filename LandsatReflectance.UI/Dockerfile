FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM nginx:alpine
WORKDIR /app
COPY --from=build /app/publish/ .
EXPOSE 5035
CMD ["nginx", "-g", "daemon off;"]
