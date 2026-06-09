# Prerequisite: docker compose up -d (from messaging/)
#
#   pwsh -File messaging/scripts/create-topics.ps1

$ErrorActionPreference = "Stop"
$messagingRoot = Split-Path -Parent $PSScriptRoot
$compose = (Resolve-Path (Join-Path $messagingRoot "docker-compose.yml")).Path
$broker = "127.0.0.1:9092"

docker compose -f $compose exec -T redpanda rpk topic delete job.catalog.events --brokers $broker 2>$null
docker compose -f $compose exec -T redpanda rpk topic delete execution.lifecycle --brokers $broker 2>$null

docker compose -f $compose exec -T redpanda rpk topic create job.catalog.events `
  --brokers $broker --partitions 6 --replicas 1 -c retention.ms=604800000

docker compose -f $compose exec -T redpanda rpk topic create execution.lifecycle `
  --brokers $broker --partitions 6 --replicas 1 -c retention.ms=604800000

docker compose -f $compose exec -T redpanda rpk topic list --brokers $broker
