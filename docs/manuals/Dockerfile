FROM python:3.12-alpine AS builder
WORKDIR /src

COPY docs/manuals/ /src/

RUN pip install --no-cache-dir mkdocs mkdocs-material
RUN mkdocs build -f mkdocs.yml -d /out

FROM nginx:alpine

COPY --from=builder /out/ /usr/share/nginx/html/

EXPOSE 80
