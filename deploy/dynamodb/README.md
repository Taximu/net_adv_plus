# DynamoDB Local bootstrap (compose)

- **`Dockerfile.init`** — small image with Python + boto3.
- **`bootstrap.py`** — waits for DynamoDB Local, creates **ExecutionQueue** and **WorkerNodes** from JSON, enables TTL on `ttl`.

No **AWS CLI** and no **AWS account** are required; boto3 only talks to the `DYNAMO_ENDPOINT` URL (DynamoDB Local inside Compose).
