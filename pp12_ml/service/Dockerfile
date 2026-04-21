FROM python:3.11-slim

WORKDIR /service

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

COPY service/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY service/app ./app
COPY data /data
COPY artifacts /artifacts

ENV PP12_DATASET_PATH=/data/construction_tasks_dataset.csv
ENV PP12_ARTIFACTS_DIR=/artifacts

CMD ["sh", "-c", "uvicorn app.main:app --host 0.0.0.0 --port ${PORT}"]