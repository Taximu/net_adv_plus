# Data Flow Diagram for UC 2.3: View Job Execution History

```plantuml
@startuml
skinparam componentStyle rectangle
skinparam defaultFontName Arial
skinparam titleFontSize 14
skinparam legendFontSize 10
left to right direction

title UC 2.3: View Job Execution History\n\nRetrieve and display historical execution records for jobs

legend top left
  <color:#e8f5e8>█</color> : End User
  <color:#e1f5fe>█</color> : System Component
  <color:#f3e5f5>█</color> : Data Storage
  <color:#fce4ec>█</color> : UI/Gateway
  <color:#e8f5e8>█</color> -- : Database Replica
end legend

' Define components with stereotypes
actor "End User\n(client)" as User <<Entity>> #e8f5e8

' UI/Gateway Layer (Left side)
rectangle "UI\nWeb Application" as UI <<UI>> #fce4ec
rectangle "CloudFront\nCDN" as CloudFront <<UI>> #fce4ec
rectangle "API Gateway\nRouting & Security" as APIGateway <<UI>> #fce4ec

' BFF Layer
rectangle "BFF Service" as BFF <<Component>> #e1f5fe

' Service Layer
rectangle "Job Manager\nService" as JobManager <<Component>> #e1f5fe
rectangle "Job Orchestrator\nService" as JobOrchestrator <<Component>> #e1f5fe

' Primary Databases (Right side)
database "Job Details DB\nRDS PostgreSQL" as JobDetailsDB <<Storage>> #f3e5f5
database "Schedule DB\nRDS PostgreSQL" as ScheduleDB <<Storage>> #f3e5f5
database "Execution History DB\nRDS PostgreSQL" as JobHistoryDB <<Storage>> #f3e5f5
database "Job Outputs DB\nS3 + Athena" as JobOutputsDB <<Storage>> #f3e5f5
database "ElastiCache\nRedis" as ElastiCache <<Storage>> #f3e5f5
database "User Preferences\nDynamoDB" as DynamoDB <<Storage>> #f3e5f5

' Database Replicas (Rightmost)
database "Job Details\nReplica" as JobDetailsReplica <<Replica>> #e8f5e8
database "Schedule DB\nReplica" as ScheduleReplica <<Replica>> #e8f5e8
database "History DB\nReplica" as HistoryReplica <<Replica>> #e8f5e8
database "Redis\nReplica" as RedisReplica <<Replica>> #e8f5e8
database "S3\nReplica" as S3Replica <<Replica>> #e8f5e8
database "DynamoDB\nReplica" as DynamoDBReplica <<Replica>> #e8f5e8

' Monitoring (Bottom)
rectangle "DataDog\nMonitoring" as CloudWatch <<Monitoring>> #fff3e0
rectangle "Monitoring\nBackup" as CloudWatchReplica <<Replica>> #e8f5e8

' Main Request Flow (Left to Right)
User -> UI : 1. Request history\nJob ID, Date Range, Filters
UI -> CloudFront : 2. Load UI assets
CloudFront -> APIGateway : 3. Forward API request
APIGateway -> BFF : 4. Route to BFF

' Metadata & Permissions Flow
BFF -> JobManager : 5. Get metadata & permissions
JobManager -> JobDetailsDB : 6. Query job details
JobDetailsDB -> JobManager : 7. Return job definitions
JobManager -> BFF : 8. Forward metadata

' History Query Flow
BFF -> JobOrchestrator : 9. Request execution history
JobOrchestrator -> ElastiCache : 10. Check cache
ElastiCache -> JobOrchestrator : 11. Return cache\n(if available)

' Alternative Cache Path
note on link
  Cache Hit: Skip 12-17
end note

' Database Query Flow (to the right)
JobOrchestrator -> ScheduleDB : 12. Query schedule info
ScheduleDB -> JobOrchestrator : 13. Return schedule

JobOrchestrator -> JobHistoryDB : 14. Query execution metadata
JobHistoryDB -> JobOrchestrator : 15. Return timing,\nstatus, metrics

JobOrchestrator -> JobOutputsDB : 16. Query detailed logs
JobOutputsDB -> JobOrchestrator : 17. Return logs,\noutputs, errors

' Cache and Compile Results (flowing back left)
JobOrchestrator -> ElastiCache : 18. Cache query results
JobOrchestrator -> BFF : 19. Compile history report

' User Preferences (to the right and back)
BFF -> DynamoDB : 20. Get user preferences
DynamoDB -> BFF : 21. Return settings\ntimezone, format

' Response Flow (back to left)
BFF -> APIGateway : 22. Format response
APIGateway -> UI : 23. Return formatted data
UI -> User : 24. Display history\ncharts, tables, logs

' Monitoring Flows (downward)
JobOrchestrator -> CloudWatch : 25. Send query metrics
BFF -> CloudWatch : 26. Send response metrics

' Replication Links (horizontal, rightward)
JobDetailsDB -[dashed]-> JobDetailsReplica : read replica
ScheduleDB -[dashed]-> ScheduleReplica : read replica
JobHistoryDB -[dashed]-> HistoryReplica : read replica
ElastiCache -[dashed]-> RedisReplica : read replica
JobOutputsDB -[dashed]-> S3Replica : cross-region
DynamoDB -[dashed]-> DynamoDBReplica : global table
CloudWatch -[dashed]-> CloudWatchReplica : backup

' Layout adjustments for better flow
User -[hidden]- UI
UI -[hidden]- CloudFront
CloudFront -[hidden]- APIGateway
APIGateway -[hidden]- BFF

BFF -[hidden]- JobManager
BFF -[hidden]- JobOrchestrator

JobManager -[hidden]- JobDetailsDB
JobOrchestrator -[hidden]- ScheduleDB
JobOrchestrator -[hidden]- JobHistoryDB
JobOrchestrator -[hidden]- JobOutputsDB
JobOrchestrator -[hidden]- ElastiCache

JobDetailsDB -[hidden]- JobDetailsReplica
ScheduleDB -[hidden]- ScheduleReplica
JobHistoryDB -[hidden]- HistoryReplica
ElastiCache -[hidden]- RedisReplica
JobOutputsDB -[hidden]- S3Replica
DynamoDB -[hidden]- DynamoDBReplica

@enduml
```
