FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /app

COPY services/farol_intelligence/pyproject.toml ./
COPY services/farol_intelligence/README.md ./
COPY services/farol_intelligence/app ./app

RUN python -m pip install --upgrade pip && python -m pip install .

EXPOSE 10000

ENTRYPOINT ["sh", "-c", "uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-10000}"]
