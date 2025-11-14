1. Which scalability option is the primary choice for high-load systems?

Horizontal Scaling is the main choice. It is about adding extra machines or nodes to a pool, providing near-limitless scalability and higher fault tolerance compared to Vertical Scaling which is limited by hardware.

2. What are the main challenges to address in horizontal scalability?

Data Consistency: Managing state and ensuring data consistency across distributed nodes (CAP theorem).

Inter-service Communication: Increased network complexity, latency, and potential for failures.

Stateless Design: Designing apps to be stateless to allow any node to handle any request, often leading to usage of cache or database.

3. Name at least 3 metrics for hardware and software to consider when designing a high-load system.

Hardware Metrics: CPU Utilization, Memory Usage, Network I/O.

Software/Application Metrics: Throughput (requests/sec), latency (response time), errors rate.

4. Give examples of domains where high-load system design is required and is not required.

Required: Social Media Platforms (VK, Telegram, Linkedin), E-commerce Marketplaces (Ozon, Wildberries), Online Payment Gateways (YooMoney, Tinkoff Checkout, Robokassa, ERIP), Video Streaming Services (Youtube, Kinopoisk, IVI.RU, VK Video).

Not Required: Internal company portals (EPAM's portals), Small Business Brochure Websites(Сайт визитка), Local Library Catalog System, Standalone Desktop Applications.




1. Classify Technical Requirements from the materials to specific NFR:

* Scalability
The system needs to handle thousands of jobs and users at the same time, and do it efficiently. The way the system is designed is with separate services like the Job Manager, Orchestrator, and Runner makes it easier to scale up on demand.

* Reliability
It’s important that jobs run on time, even if there’s sudden spike in activity or if part of the system is not working perfectly. That’s what reliability and availability are all about.

* Real-time Monitoring
Users should be able to see what’s is going with their jobs as it happens. This means the system has to process data quickly (for good performance) and show it in a way that’s easy to understand (for usability).

* Failure Notifications
If something goes wrong users need to know immediately. This not only helps keep the system reliable, but also makes it more user-friendly.

* Flexibility
The system should be able to handle all sorts of jobs—one-time, recurring, running at the same time, or jobs that need to run alone. This flexibility means the system can adapt to whatever is needed.

* Integration
It should be easy to connect the system with other tools and services, so it can handle even the most complex jobs. This is key for working well with other systems and for future growth.

* Security
All data (sent or stored) needs to be protected. This is a straightforward security requirement.

* Cost Efficiency
The system should deliver good performance without running up unnecessary costs.

2. Which NFRs you would additionally consider?

* Maintainability
The system will need to change over time—new job types, new features, new ways to notify users. The code and architecture should make it easy to update, test, and deploy changes. This means having clear APIs, well-organized data, and good logging.

* Fault Tolerance & Resilience
It’s not just about being reliable; the system should also bounce back if something goes wrong. For example, if a Job Runner crashes, the system should recover without losing any data. Things like retries, circuit breakers, and dead-letter queues help make the system more robust.

* Observability
Monitoring is more than just checking logs. Observability means being able to trace a job’s path through the system, see detailed metrics (like how many jobs run per minute or how often they fail), and really understand what’s happening so you can fix issues quickly.

* Performance (Latency & Throughput)
Scalability is about handling more work, but performance is about how fast things happen.

How quickly a job starts after it’s scheduled? How many jobs the orchestrator can handle per second? How fast the web app loads job lists and logs?

The system should be easy for operations teams to manage. This means: Simple deployment (using containers like Docker or Kubernetes). Easy configuration for different environments. Health check endpoints so tools can monitor the status of each component. Data Integrity and Consistency. It’s crucial that jobs aren’t lost or accidentally run more than once (unless that’s intended). The system’s data stores need to handle state changes (like scheduling, running, and completing jobs) in a way that prevents errors or race conditions.