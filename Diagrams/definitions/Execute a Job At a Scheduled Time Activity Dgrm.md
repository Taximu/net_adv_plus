# Execute job at scheduled time - activity diagram

``` mermaid
flowchart TD
    Start([Scheduled Time Reached]) --> A[Job Orchestrator: <br/>Fetch Due Jobs from Orchestrator DB]

    A --> B[Split Jobs into <br/>Parallel Batches]

    B --> C{For each: Acquire Lock?}
    C -->|Yes| D[Attempt to Lock Job <br/>in Database]
    C -->|No| E[Proceed to Execution]

    D --> F{Lock Acquired?}
    F -->|No| G[Log & Skip Execution<br/>Job is already running]
    F -->|Yes| E

    subgraph E [Execute Job via Runner]
        direction TB
        H[Job Runner: <br/>Trigger Execution via Queue] --> I[Execute Integration Logic<br/>e.g., API Call, DB Query]
        I --> J{Integration <br/>Successful?}
        J -->|No| K[Mark Execution as Failed]
        J -->|Yes| L[Mark Execution as Success]
        K & L --> M[Job Runner: <br/>Stream Output & Logs]
    end

    M --> N{Was Job Locked?}
    N -->|Yes| O[Release Job Lock <br/>in Database]
    N -->|No| P[Update Final Job Status<br/>in Database]

    O --> P

    P --> Q{Execution Status?}
    Q -->|Success| R([End - Success])
    Q -->|Failure| S[Trigger Notification Flow<br/>UC3.2]

    S --> R
    G --> R
```
