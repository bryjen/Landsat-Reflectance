FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# TODO: Find a way to use nginx to serve down the line
FROM node:alpine AS final
WORKDIR /app
COPY --from=build /app .

RUN npm install -g serve
EXPOSE 8080
CMD ["serve", "-s", "wwwroot", "-l", "8080"]
