FROM python:3.12-slim AS builder
WORKDIR /src

COPY . /src/

RUN pip install --no-cache-dir --only-binary :all: -r requirements.lock
RUN mkdocs build -f mkdocs.yml -d /out

FROM nginx:alpine

COPY --from=builder /out/ /usr/share/nginx/html/

EXPOSE 80
