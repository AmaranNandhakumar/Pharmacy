# Pharmacy web app: build the Angular bundle, then serve it with nginx (which also forwards /api to the API)
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/pharmacy-frontend/browser /usr/share/nginx/html
EXPOSE 80
