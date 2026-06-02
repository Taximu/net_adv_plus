# DynamoDB scripts (UC 2.1)

Table setup and **Docker Compose for DynamoDB Local** now live next to the DAL:

`JobScheduler.DAL/src/JobScheduler.DAL/docker-compose-dynamodb.yml`  
`JobScheduler.DAL/src/JobScheduler.DAL/setup-dynamodb.ps1`  
`JobScheduler.DAL/src/JobScheduler.DAL/setup-dynamodb-local.ps1`

Run the local script from **`JobScheduler.DAL/src/JobScheduler.DAL`** (or pass the compose path explicitly). This folder can keep schema reference: `../db_scripts/Schema2_Job_Execution_Queue(NoSQL - DynamoDB).json`.
