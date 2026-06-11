"""Create DynamoDB Local tables (ExecutionQueue, WorkerNodes) — no AWS CLI required."""
from __future__ import annotations

import json
import os
import sys
import time
from pathlib import Path

import boto3
from botocore.exceptions import ClientError

DIR = Path("/bootstrap")
ENDPOINT = os.environ.get("DYNAMO_ENDPOINT", "http://dynamodb-local:8000")


def make_client():
    return boto3.client(
        "dynamodb",
        endpoint_url=ENDPOINT,
        region_name="us-east-1",
        aws_access_key_id="local",
        aws_secret_access_key="local",
    )


def wait_for_dynamo(client) -> None:
    print(f"Waiting for DynamoDB at {ENDPOINT}...")
    for i in range(90):
        try:
            client.list_tables()
            return
        except Exception:
            if i == 89:
                raise
            time.sleep(1)


def load_create_kwargs(filename: str) -> dict:
    with (DIR / filename).open(encoding="utf-8") as f:
        return json.load(f)


def create_if_missing(client, table_name: str, json_file: str) -> None:
    existing = client.list_tables()["TableNames"]
    if table_name in existing:
        print(f"{table_name} already exists")
        return
    print(f"Creating {table_name}...")
    kwargs = load_create_kwargs(json_file)
    try:
        client.create_table(**kwargs)
    except ClientError as ex:
        if ex.response["Error"]["Code"] == "ResourceInUseException":
            print(f"{table_name} already exists (race)")
            return
        raise
    waiter = client.get_waiter("table_exists")
    waiter.wait(TableName=table_name)


def main() -> int:
    client = make_client()
    wait_for_dynamo(client)
    create_if_missing(client, "ExecutionQueue", "create-execution-queue.json")
    create_if_missing(client, "WorkerNodes", "create-worker-nodes.json")
    try:
        client.update_time_to_live(
            TableName="ExecutionQueue",
            TimeToLiveSpecification={"Enabled": True, "AttributeName": "ttl"},
        )
    except ClientError as ex:
        print(f"UpdateTimeToLive (may already be set): {ex.response['Error']['Message']}")
    print("DynamoDB bootstrap complete.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
