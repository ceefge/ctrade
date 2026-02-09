FROM python:3-alpine

RUN apk add --no-cache git && \
    git clone --depth 1 https://github.com/novnc/noVNC.git /opt/noVNC && \
    pip install --no-cache-dir numpy websockify && \
    apk del git

EXPOSE 8080

CMD ["websockify", "--web", "/opt/noVNC", "8080", "ib-gateway:5900"]
