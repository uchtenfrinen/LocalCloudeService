FROM golang:1.22-alpine AS build
WORKDIR /src
COPY go.mod ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 go build -o /out/server .

FROM alpine:3.20
WORKDIR /app
COPY --from=build /out/server /app/server
VOLUME ["/app/data"]
EXPOSE 8080
ENTRYPOINT ["/app/server"]
