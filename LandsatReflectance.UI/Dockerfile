FROM node:alpine AS build_css
WORKDIR /src
COPY . .
RUN npm install tailwindcss@3.4.17 --save-dev
RUN ls
RUN npx tailwindcss -i ./LandsatReflectance.UI/wwwroot/css/app.css -o ./LandsatReflectance.UI/wwwroot/css/app.min.css -c ./LandsatReflectance.UI/tailwind.config.js

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY --from=build_css /src .
RUN dotnet publish LandsatReflectance.UI -c Release -o /app

# TODO: Find a way to use nginx to serve down the line
FROM node:alpine AS final
WORKDIR /app
COPY --from=build /app .

RUN npm install -g serve
EXPOSE 8080
CMD ["serve", "-s", "wwwroot", "-l", "8080"]
