# Communication Patterns Between Components

## Frontend to Backend

- **User to CloudFront:** Users access the web application via CloudFront, which delivers static assets (HTML, CSS, JS) from edge locations for low latency.
- **CloudFront to API Gateway:** Dynamic API requests are proxied through Amazon API Gateway, which handles request routing, authorization, and rate limiting.
- **API Gateway to BFF:** All API requests are routed to the Backend for Frontend (BFF) service. The BFF aggregates data from various backend services and provides a client-specific API, simplifying the frontend logic.

## Job Management Flow

- **BFF to Job Manager:** The BFF makes synchronous REST API calls to the Job Manager for all job-related CRUD operations (Create, Read, Update, Delete).
- **Job Manager to Job Details DB:** The Job Manager persists and retrieves job definitions and metadata from the Amazon RDS (PostgreSQL) database, using connection pooling for efficiency.
- **Job Manager to Job Orchestrator:** When a job is ready for execution, the Job Manager sends a job snapshot to the Job Orchestrator via an internal API call to initiate the scheduling process.

## Job Orchestration & Execution Flow

- **Job Orchestrator to Schedule DB:** The Job Orchestrator manages execution schedules, triggers, and state in the RDS PostgreSQL database, using transactions to ensure consistency and prevent race conditions.
- **Job Orchestrator to Job Instance Queue:** When a job is due to run, the Orchestrator places a message into the Amazon SQS (Standard) queue, which acts as a buffer and decouples orchestration from execution.
- **Job Instance Queue to Job Runner:** An auto-scaling group of Job Runner service instances polls the SQS queue. They consume messages, which triggers the execution of the actual job logic.
- **Job Runner to External Integrations:** The Job Runner performs the core business logic, typically involving HTTP/HTTPS calls to external third-party APIs and services, with built-in retry and backoff mechanisms for resilience.
- **Job Runner to Job Outputs DB:** Execution results, logs, and output data are written in batches to Amazon S3. Amazon Athena is used on top of S3 to enable SQL-based querying of this data.
- **Job Runner to ElastiCache:** The Job Runner uses Amazon ElastiCache (Redis) for caching frequently accessed reference data or to store intermediate state, reducing latency and load on downstream services.

## Notification Flow

- **Job Orchestrator to Notification Queue:** The Job Orchestrator places failure events and other critical notifications into an Amazon SQS FIFO queue. The FIFO queue guarantees exactly-once processing and preserves the order of events.
- **Notification Queue to Notification Lambda:** The SQS FIFO queue triggers an AWS Lambda function for each message, enabling an event-driven, serverless approach to notifications.
- **Notification Lambda to Delivery Channels:** The Lambda function fetches user preferences from DynamoDB and makes parallel, asynchronous API calls to deliver notifications via Amazon SES (email), Slack, and Microsoft Teams.

## Data Query & Reporting Flow

- **BFF to Job Reporter:** For status checks and historical reporting, the BFF queries the dedicated Job Reporter service via a REST API.
- **Job Reporter to Job Outputs DB:** The Job Reporter executes SQL queries using Amazon Athena to analyze job execution data stored in S3.
- **Job Reporter to ElastiCache:** The Job Reporter checks the Redis cache first for frequently requested data to reduce query latency and cost.

## Monitoring & Observability Flow

- **All Services to CloudWatch/DataDog:** All ECS services and the Lambda function emit custom metrics and structured logs. This data is collected by CloudWatch and/or integrated with DataDog for centralized monitoring, alerting, and performance analysis.

---

### Reasoning for Architecture Selection

**CloudFront + API Gateway:**

- **CloudFront:** Provides global content delivery, DDoS mitigation, and performance optimization for static assets via edge caching.
- **API Gateway:** Offers a fully managed entry point for APIs, providing security (WAF, IAM), throttling, and request/response transformation.

**Backend for Frontend (BFF) Pattern:**

- **ECS Fargate for BFF:** Allows for client-specific API aggregation and orchestration, insulating the frontend from the complexity of the backend microservices. Fargate eliminates the need to manage servers, providing a good balance of control and operational simplicity.

**Database & Storage Selection:**

- **RDS PostgreSQL:** Chosen for the Job Details and Schedule DBs due to its strong consistency (ACID compliance), relational data model, and support for complex queries and transactions.
- **DynamoDB:** Ideal for the User Preferences DB due to its serverless nature, high scalability for read/write operations, and flexible schema.
- **S3 + Athena:** The optimal choice for storing vast amounts of job outputs and logs. S3 provides durable, unlimited storage, while Athena enables serverless SQL querying without managing any infrastructure.

**Messaging & Decoupling Patterns:**

- **SQS Standard Queue:** Used for job instances to decouple the Job Orchestrator from the Job Runners. This allows for independent scaling and provides a buffer to handle load spikes, with built-in retries on failure.
- **SQS FIFO Queue:** Used for notifications to ensure critical alerts are processed exactly once and in the correct order, which is essential for auditing and user trust.

**Compute Services:**

- **ECS Fargate:** Used for long-running services (BFF, Job Manager, Orchestrator, Reporter, Runner) that require persistent HTTP connections, consistent performance, and deep VPC integration.
- **AWS Lambda:** The perfect fit for the event-driven Notification Service, as it scales automatically from zero and incurs costs only when messages are processed.

**Caching Strategy:**

- **ElastiCache Redis:** Delivers in-memory performance for caching frequently accessed job data and user sessions, significantly reducing latency and load on the primary databases.

---

### Scalability & Reliability Highlights

- **Elastic Scaling:** Every component can scale independently.
  - **ECS Services** can scale based on CPU/Memory usage or custom metrics (e.g., SQS queue depth for Job Runners).
  - **Lambda** scales automatically with the SQS FIFO queue load.
  - **SQS** provides a virtually unlimited buffer for messages.
  - **RDS** can be scaled vertically and can use read replicas.
  - **DynamoDB** scales seamlessly with demand.

- **Fault Tolerance:**
  - The use of queues decouples services, preventing cascading failures.
  - The Job Instance Queue persists messages, so no jobs are lost if the Job Runner service is temporarily down.
  - The FIFO queue ensures reliable, ordered notification delivery.

- **Operational Excellence:**
  - Centralized logging and monitoring with DataDog/CloudWatch provide full observability into the system's health and performance.
  - The use of managed AWS services (Fargate, RDS, SQS, etc.) reduces the operational overhead of patching, provisioning, and scaling infrastructure.

PS> Unfortunately I am not able to give calculations with numbers like asked at System Design interview for some Big Tech Company (1M RPC or 10M Users, Petabyte of data and e.t.c.).
So this diagram is more of fantasy and hope that Amazon services will work the way they claim but all this has to be checked during real load or with some load testing.
