FROM python:alpine

WORKDIR /app

COPY requirements.txt .

ENV PIP_ROOT_USER_ACTION=ignore
RUN pip install --upgrade pip
RUN pip install --upgrade --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 80

RUN apk add --no-cache curl

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
    CMD curl --silent --fail http://localhost/health || exit 1

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "80"]