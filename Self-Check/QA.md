# Module 1

1.Which scalability option is the primary choice for high-load systems?

Horizontal Scaling is the main choice. It is about adding extra machines or nodes to a pool, providing near-limitless scalability and higher fault tolerance compared to Vertical Scaling which is limited by hardware.

2.What are the main challenges to address in horizontal scalability?

Data Consistency: Managing state and ensuring data consistency across distributed nodes (CAP theorem).

Inter-service Communication: Increased network complexity, latency, and potential for failures.

Stateless Design: Designing apps to be stateless to allow any node to handle any request, often leading to usage of cache or database.

3.Name at least 3 metrics for hardware and software to consider when designing a high-load system.

Hardware Metrics: CPU Utilization, Memory Usage, Network I/O.

Software/Application Metrics: Throughput (requests/sec), latency (response time), errors rate.

4.Give examples of domains where high-load system design is required and is not required.

Required: Social Media Platforms (VK, Telegram, Linkedin), E-commerce Marketplaces (Ozon, Wildberries), Online Payment Gateways (YooMoney, Tinkoff Checkout, Robokassa, ERIP), Video Streaming Services (Youtube, Kinopoisk, IVI.RU, VK Video).

Not Required: Internal company portals (EPAM's portals), Small Business Brochure Websites(Сайт визитка), Local Library Catalog System, Standalone Desktop Applications.

## Module 2

1.Classify Technical Requirements from the materials to specific NFR:

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

2.Which NFRs you would additionally consider?

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

## Module 3

### Data Layer Capabilities

* What alternatives exist for row-based databases? What does the term NoSQL mean?

The main alternatives are NoSQL databases. It's a family of databases designed for different jobs, especially when you're dealing with massive scale, speed, or unstructured/semi-structured data.
NoSQL means that it is not only SQL.

The main types are:

Document Databases: Like MongoDB. Instead of rows, they store data in "documents" (often in JSON format). Imagine storing a full user profile, with all their preferences and address, as a single self-contained document, not broken up across multiple tables. It's flexible and fast for certain kinds of applications.

Key-Value Stores: Like Redis. Super simple and incredibly fast. It's just a giant hash table. Perfect for caching or temporary data.

Column-Family Stores: Like Cassandra. Instead of storing data by rows, it stores it by columns. This is fantastic for analyzing huge datasets where you need to read a few columns across millions of rows very quickly.

Graph Databases: Like Neo4j. These are for when the relationships between data points are the most important thing. Perfect for social networks, fraud detection, or recommendation engines.

* What are the main differences between a Database, Data Warehouse, and Data Lake?

Database: This is your operational garage. It's designed for online transactional data. It's optimized for reading and writing single, specific items. This is where your live application data lives.

Data Warehouse: This is your analysis database. You store the data in OLAP cubes and run complex reports, create dashboards, and perform dimensioanl queries. It's structured, historical, and optimized for complex queries, not for fast transactions.

Data Lake: This is the raw here—structured data, semi-structured logs, unstructured images, social media feeds—in its native format. You don't need to structure it first. The power is in its flexibility; you can later decide how to cut and use the lumber. It's great for big data analytics, machine learning, and exploring data where you don't yet know what you're looking for.

* How can you determine if your data is structured, semi-structured, or unstructured?

Structured Data: It's rigid and predictable. Think of a spreadsheet or a database table with clear columns. You always know what to expect.

Semi-Structured Data: It's in a box, but the items inside are a bit loose. It has some organization (tags, markers, or keys) but the structure is flexible. The most common example is JSON or XML. A JSON object for a user might have name and email, but also a list of hobbies that can vary for each person. It's self-describing.

Unstructured Data: This is just a pile of stuff on a table. There's no inherent organization that a computer can easily understand. Examples are video files, audio recordings, images, PDF documents, and emails. The content is rich, but to analyze it, you need complex tools like AI to extract meaning from it.

### Application Layer Capabilities

* What are the main characteristics of a Service-Oriented Architecture?

A Service-Oriented Architecture (SOA) organizes software as a group of independent, specialized "services" that communicate over a network to perform business tasks. Think of it as a system where each service is like a skilled team member, each responsible for a specific job, but they all work together to get things done.

Key characteristics:

Reusability: Services are designed to be reusable. For instance, an "Accounting Service" can support multiple apps, like an e-commerce platform and an internal payroll system. No need to reinvent the wheel.
Interoperability: Services can communicate seamlessly using standardized protocols (e.g., HTTP, SOAP, gRPC, WebSockets), no matter what programming language they’re built in.
Loose Coupling: Services are independent. Changes in one part of the system (like the HR department) won’t disrupt another (like Accounting).
Central "Bus": Many SOA systems use an Enterprise Service Bus (ESB) to act as the central hub for communication between services.

* Can you explain the difference between Message-Driven and Event-Driven architectures?

Message-Driven: This is like sending a direct command. There’s a clear sender and receiver, and the message is an instruction (e.g., "Do X now").
Event-Driven: This is more like broadcasting a news alert. A service announces something that happened (the "event"), and any other service that’s subscribed to that event can react to it. The original service has no idea who’s listening or what they’ll do—it just shares the news.

* What are the benefits of Asynchronous Message-Based Communication in a microservices architecture?

Asynchronous communication is like saying, "I’ll leave you a note, and I’m not waiting around for your reply." If Service A sends a message to Service B asynchronously, it doesn’t pause its work—it just moves on to the next task. This approach brings some big advantages:

Decoupling: Services don’t need to be up and running at the same time. For example, if the "Email Service" is down, the "Order Service" can still place orders and queue up "Send Email" messages for later.
Resilience: It prevents one slow or failing service from dragging down the whole system.
Scalability: If there’s a surge in demand, you can scale up the number of "worker" services to process a backlog of messages faster.

* What is a Serverless Architecture and what are its advantages and disadvantages?

"Serverless" doesn’t mean there are no servers—it just means you don’t have to manage them. Think of it like this:

Traditional Setup: You rent a pizza oven 24/7. You pay for it even when you’re not baking pizzas, and you’re responsible for cleaning and maintenance.
Serverless: You use a shared kitchen where you only pay for the time you spend making pizzas (or running your code). You don’t worry about the oven, electricity, or upkeep.
Advantages:

No server management—your cloud provider takes care of scaling, patching, and infrastructure.
Cost efficiency—you only pay when your code runs. If it’s idle, you pay nothing.
Automatic scaling—it handles spikes in demand effortlessly, from zero to thousands of requests.
Disadvantages:

Cold Starts: If your function hasn’t run in a while, there might be a slight delay as the environment "wakes up."
Vendor Lock-In: Your code may depend heavily on specific cloud services, making it harder to switch providers.
Debugging Challenges: Breaking your system into tiny, short-lived functions can make troubleshooting more complex.

* How does a Hybrid Architecture leverage the strengths of different architectural styles?

A hybrid architecture is about using the right tool for the job—it’s like building a house with both a hammer and a screwdriver.

For example:

Use microservices for the core of your application to make it flexible and scalable.
Use a monolithic design for something simple, like an internal reporting tool, where speed of development matters more than scalability.
Use serverless functions for specific tasks, like processing image uploads or running scheduled jobs.
The strength of a hybrid approach lies in its flexibility—it avoids a "one-size-fits-all" mindset and tailors each part of the system to its unique needs.

* What is the main differences in SOA in comparison with microservices architecture?

Granularity: SOA focuses on larger, reusable services that handle different business processes, while microservices are smaller, more focused services designed for specific tasks.

Independence: Microservices are fully independent, with their own databases and deployment pipelines, whereas SOA services often share resources like a central database or an Enterprise Service Bus (ESB).

Communication: SOA typically relies on heavier protocols like SOAP, XML and REST, while microservices favor lightweight protocols like HTTP/REST or gRPC. But no one said that it is not possible to build microservices with SOAP protocol as communication, it takes some time but it is possible.

Flexibility: Microservices are more agile and scalable due to their independent nature, making them easier to develop, deploy, and scale individually. SOA has some parts that are monolithic and better suited for enterprise systems with tightly integrated components. Moreover microservices are for professional developers whereas SOA is for any kind, easier to build.

### Infrastructure Layer Capabilities

* What are the key components of web infrastructure design?

-DNS (Domain Name System): This is like a phonebook. It translates "www.epam.com" into an IP address (e.g., 192.0.2.1) so browsers know where to find your servers.
-Load Balancer: The traffic cop that distributes incoming requests across multiple servers to prevent any one server from being overwhelmed.
-Web/Application Servers: These are the "kitchen staff" that run your app’s code and generate web pages.
-CDN (Content Delivery Network): A network of local "delivery depots" that store static content (like images or videos) closer to users for faster loading times.
-Database: The central filing cabinet where your app’s data lives.
-Caching Layer (e.g., Redis): A super-fast notepad that stores frequently accessed data to avoid repeated trips to the database.
-Firewalls / WAF: The security guards that monitor and block malicious traffic.

* What differs HTTP 3.0 from earlier versions?

The big change in HTTP/3 is how data flows between a user and a server. Here’s a quick breakdown:

HTTP/1.1: Imagine a single-lane road where each car (request) has to wait for the one in front to finish. If one packet gets lost, everything behind it is stuck.
HTTP/2: This introduced multiple lanes on the same road, so multiple requests could travel at once. But if one lane had an issue, it still caused delays.
HTTP/3: This is a whole new road. Instead of using the older TCP protocol, it uses QUIC, which is based on UDP. It’s faster and smarter—if one "lane" has trouble, the others keep moving without delay. This makes it especially great for mobile or unreliable networks.

* What are the roles of a Load Balancer, a Reverse Proxy, and an API Gateway in a web infrastructure? How do they differ from each other?

Load Balancer: Think of this as a traffic manager. It distributes incoming requests across multiple servers to prevent overload and ensure reliability.
Reverse Proxy: This is like the public spokesperson for your servers. It can handle tasks like SSL termination (decrypting HTTPS traffic), caching, and compressing responses to improve performance.
API Gateway: The concierge for microservices. It manages communication between clients and multiple backend services by handling routing, authentication, rate limiting, and more.

* What is the purpose of DNS Load Balancing and how does it work?

DNS load balancing is like directing traffic before it even reaches your servers. When your browser asks, "Where’s www.epam.com ?", the DNS server doesn’t always give the same answer. Instead, it rotates through multiple server IP addresses.

This helps distribute traffic across different servers or data centers, ensuring no single one gets overwhelmed. It’s a simple but effective way to balance the load right from the start.

* What role does a CDN play in load balancing?

A CDN (Content Delivery Network) is like an offloading expert. It helps reduce the strain on your main servers by handling static content, such as images, videos, and CSS files. When a user requests something, the CDN serves it from an "edge server" near their location, which makes everything faster and more efficient.

This doesn’t replace a traditional load balancer, but it complements it by taking a significant portion of the workload off your main servers.

* What is the function of a Web Application Firewall (WAF) in a web infrastructure?

Think of a WAF as a specialized security guard that monitors the content of incoming traffic, not just the source. It protects your web app from attacks that a regular firewall might miss, including:

SQL Injection: Blocking malicious database commands.
Cross-Site Scripting (XSS): Preventing attackers from injecting harmful scripts into web pages.
Cross-Site Request Forgery (CSRF): Stopping attackers from tricking users into performing unintended actions.
By sitting between the internet and your web app, a WAF acts as a critical layer of defense against common web-based threats.

## Module 4

### ACID Transactions

1. What does each component of ACID (Atomicity, Consistency, Isolation, Durability) ensure in a transaction?

-**Atomicity**: The whole transaction happens, or none of it does. No partial updates.  
-**Consistency**: Transactions keep the database in a valid state—rules, constraints stay intact.  
-**Isolation**: Concurrent transactions don’t step on each other’s toes.  
-**Durability**: Once committed, the transaction survives crashes.

2. How does atomicity guarantee that a transaction is treated as an indivisible unit?

It uses undo/rollback mechanisms. If anything fails mid-transaction, all changes are reverted, like it never started.

3. What are the key differences between single-object and multi-object transactions, and why do these differences matter in system design?

Single-object: only one item (e.g., a key-value). Multi-object: involves multiple items/tables. Multi-object is harder—needs coordination, locking, and can hurt performance, but it’s essential for correctness in things like moving money between accounts.

4. List and explain the various isolation levels available in DBMS and their impact on concurrent transactions.

-**Read Uncommitted**: Can read uncommitted data → dirty reads.  
-**Read Committed**: Only read committed data, but non-repeatable reads happen.  
-**Repeatable Read**: Same row stays consistent within transaction, but phantoms (new rows) can appear.  
-**Serializable**: Full isolation—transactions act like they run one at a time.

5. How do different isolation levels affect the occurrence of issues such as dirty reads, non-repeatable reads, and phantom reads?
-Dirty reads: only possible in Read Uncommitted.  
-Non-repeatable reads: prevented starting at Repeatable Read.  
-Phantom reads: only fully prevented in Serializable.

6. In a distributed system, what trade-offs might you face when choosing a higher isolation level versus a lower one?

Higher isolation (like Serializable) means more locking, coordination, and latency. In distributed systems, that can kill availability and speed. Lower isolation improves performance but risks weird concurrency bugs.

### Conceptual Understanding (Basics)

1. What do each of the three letters in CAP (Consistency, Availability, Partition Tolerance) stand for, and what does each term mean?

-**Consistency**: All nodes see the same data at the same time.  
-**Availability**: Every request gets a response (success or failure).  
-**Partition Tolerance**: System keeps working despite network splits.

2. In your own words, state the CAP theorem. What does it assert about the ability of a distributed system to provide consistency, availability, and partition tolerance simultaneously?

You can’t have all three at once in a distributed system when a network partition happens. You must choose between Consistency and Availability during the split.

3. According to the CAP theorem, what trade-off must a system make when a network partition occurs?

-**Choose C**: Stop responding from some nodes (lose Availability) to keep data consistent.  
-**Choose A**: Keep responding but might serve stale/inconsistent data.

4. Why is Partition Tolerance often considered mandatory in real distributed systems?

In real systems, networks are unreliable. You can’t assume partitions won’t happen, so you must design for them. P is basically non-negotiable.

5. The “choose any two” phrasing of CAP is sometimes seen as oversimplified. Why?

Because you only really choose between C and A *during a partition*. In normal operation, you can aim for both. Also, it’s not all-or-nothing—there are shades of consistency and availability.

### Trade-Offs and Real-World Design Considerations

1. If a distributed system guarantees consistency and availability, what happens during a network partition?

It can’t—if there’s a true partition, you must sacrifice either C or A. “CA” systems usually assume no partitions, which isn’t realistic in distributed networks.

2. Give an example of a real-world CP system. Why does it choose consistency over availability?

**ZooKeeper**. It chooses consistency because coordination services need exact agreement; it’ll become unavailable if it can’t guarantee it.

3. Give an example of a real-world AP system. Why does it prioritize availability over consistency?

**Cassandra** (default config). Prioritizes availability—keeps taking writes/reads even during partitions, but data may conflict temporarily.

4. How does a CP system differ from an AP system during a partition in terms of user experience?

CP: User sees errors or timeouts. AP: User can keep using the app but might read stale data or have later conflicts.

5. What factors should you consider when deciding between CP and AP for a distributed application?

-Business impact of stale vs. unavailable data.  
-Whether conflicts can be resolved later.  
-Latency requirements.  
-User expectations (banking vs. social media).

### Practical Application Scenarios

1. Online banking system — prioritize consistency or availability under a partition?

Must be consistent. Better to reject transactions than show wrong balance.  

2. Social media feed — is it better to show stale data or disable the feed during a partition?

Show stale data. Users prefer seeing old posts over “try again later.”  

3. E-commerce shopping cart replication under partition — allow updates or block them?

Let users add items locally, merge later. Blocking cart updates loses sales.

4. Configuration service scenario — continue with stale config or block usage until updated?

Stale config can cause broken behavior; better to fail clearly until updated config is consistent.

### Advanced Topics: PACELC and Dynamic Adjustments

1. What is the PACELC theorem, and how does it extend CAP?

It extends CAP: *If Partition (P), choose between Availability and Consistency (A/C); Else (E), choose between Latency and Consistency (L/C)*. Acknowledges there’s a trade-off even without partitions.

2. What is the latency vs. consistency trade-off in PACELC?

Strong consistency often means higher latency (waiting for coordination). Weak consistency gives faster responses.

3. What is tunable consistency, and how does it help adjust CAP trade-offs?

Let the app decide per operation: e.g., read/write with quorum for strong consistency, or with one node for low latency. Used in Dynamo-style systems.

4. How might a system dynamically change CAP preferences when network conditions change?

Systems can detect network health and switch modes: e.g., strong consistency when network is fine, relax to available mode during partitions, or adjust quorum sizes. Helps balance based on current conditions.

## Module 4_2

### 1. Replication Lag

1. What is replication lag, and why does it occur?

The delay between a write on the leader and its application on a follower. Occurs due to network latency, follower load, or single-threaded log apply.

2. What is one way to detect replication lag in a production environment?

Monitor seconds_behind_master (MySQL) or pg_stat_replication.replay_lag (PostgreSQL).

3. Name one strategy to mitigate the negative impact of replication lag.

Read from the leader for critical or recently updated data; use followers only for tolerant reads.

4. What is a potential application-level consequence of replication lag?

Stale reads (e.g., user sees their own comment missing after a refresh).

### 2. Leader-Follower (Master-Slave) Replication

1. In a leader-follower replication setup, why might an application choose asynchronous replication over synchronous replication?

Performance — synchronous replication adds write latency and risk of unavailability if a follower fails.

2. How is failover typically handled if the leader fails in a leader-follower system?

Promote a follower to leader; update application/database config to point to the new leader.

3. What is one major advantage of leader-follower replication?

Strongly consistent reads from the leader, and horizontal read scaling via followers.

4. Give one reason an organization might still prefer a single-leader approach despite scalability concerns.

Simplicity — no conflict resolution logic needed, easier application code.

### 3. Multi-Leader Replication

1. In multi-leader replication, why do conflicts occur more frequently than in leader-follower systems?

Writes can occur concurrently on different leaders, leading to conflicting updates on the same data.

2. How can applications handle conflicting writes in a multi-leader setup?

Conflict-free replicated data types (CRDTs), last-write-wins (LWW), or application-specific merge logic.

3. What is a typical use case for multi-leader replication?

Multi-datacenter deployments (writes local to each region, asynchronous cross-replication).

4. What is the main reason some systems choose multi-leader replication despite the complexity of conflict resolution?

Lower write latency for globally distributed users (write locally, replicate async).

### 4. Leaderless Replication

1. How does leaderless replication achieve consistency without a single leader node?

Uses quorums (e.g., W + R > N) where reads/writes go to multiple replicas without a central coordinator.

2. What role does "hinted handoff" play in leaderless replication systems?

A replica temporarily stores writes for a down node and replays them when it recovers.

3. What is read repair, and why is it important in leaderless replication?

During a read, the client checks multiple replicas and updates stale ones — repairs inconsistency proactively.

4. How does setting W = N (where N is the total number of replicas) impact write availability in a leaderless system?

Write availability drops if any replica is down (W = N requires all replicas to succeed). Trades availability for consistency.

### 5. Practical Replication Considerations

1. Which replication strategy (leader-follower, multi-leader, or leaderless) typically prioritizes availability over strong consistency?

Leaderless replication (with quorums tuned for availability) and some multi-leader async setups.


### 6. Partitioning and sharding

## 1. Partitioning

1. What is data partitioning and why is it important in distributed systems?

Data partitioning means splitting a dataset (usually one logical table or collection) into smaller pieces that can be stored, moved, and queried more independently—either within one database (e.g. PostgreSQL declarative partitions) or across nodes when combined with sharding. In distributed systems it matters because it reduces how much data each node must hold and scan, enables parallelism, improves fault isolation (a partition failure affects less data), and is often a prerequisite for scaling out while keeping growth manageable.

2. How do horizontal and vertical partitioning differ in terms of data organization and use cases?

Horizontal partitioning splits **rows**: the same schema is repeated across partitions (e.g. by range, hash, or tenant), and each partition holds a subset of rows. It is used for scale-out, time-series retention, and isolating hot subsets of data. Vertical partitioning splits **columns** (or groups of columns): different attributes live in different tables or stores (e.g. wide profile JSON in a document store, billing columns in SQL). It is used to reduce row width, match access patterns, or place seldom-used columns on cheaper storage.

3. Compare range-based and hash-based partitioning strategies. When would you choose one over the other?

Range partitioning assigns rows to partitions by contiguous key intervals (e.g. order date by month). It excels at **time-window queries** and **efficient archival** (drop old partitions) but can create **hot partitions** if new data always hits the “latest” range. Hash partitioning maps keys through a hash function into a fixed number of buckets, spreading load more **evenly** when keys are high-cardinality and well distributed; it is weaker when you need **range scans across few partitions** unless the hash key aligns with access patterns. Choose **range** for temporal locality and lifecycle; choose **hash** for write spread and even size when point lookups by that key dominate.

4. What are the benefits of using a hybrid approach that combines partitioning with replication?

Replication provides **read scale** and **high availability** (standbys, failover); partitioning provides **data locality and manageability** within each replica’s dataset. Together, each replica can host the same partition layout so applications keep a simple model while **read replicas** serve historical or reporting traffic, the **primary** handles writes, and **partition maintenance** (detach/drop, vacuum per child) is cheaper than one monolithic table. You also get better use of I/O and cache per partition on each node.

5. In what ways can partitioning improve query performance and scalability?

Partition **pruning** lets the optimizer skip irrelevant child tables when the query predicate matches the partition key, cutting I/O. Smaller indexes and tables per partition speed **builds, backups, and reindexes**. Writes and reads can be **parallelized** across partitions where the engine supports it. Operationally, partitions bound **blast radius** and allow **rolling** data lifecycle policies— all of which improve perceived scalability and steady-state latency under growth.

### 2. Sharding

1. How is sharding defined, and how does it relate to horizontal partitioning?

Sharding is **horizontal partitioning applied across multiple independent database instances** (shards), each owning a disjoint subset of data, with routing logic in the application, proxy, or driver to pick the right shard. Conceptually it is the same “split rows by key” idea as horizontal partitioning, but the physical boundary is **separate servers** with separate connection endpoints, so cross-shard queries and transactions become hard or expensive.

2. What factors should you consider when deciding to shard a database?

Consider **data and traffic size** (working set, QPS, storage), **hot spots** and skew, **latency and SLA** needs, **team maturity** (runbooks, observability), **loss of cross-shard joins and foreign keys**, **resharding** and **rebalancing** cost, **multi-tenant isolation** requirements, and whether **read replicas**, **caching**, **partitioning**, or **vertical scale** are still cheaper than shard complexity.

3. What are the main alternatives to sharding for scaling a database, and when might these alternatives be preferable to implementing sharding?

Alternatives include **vertical scaling** (bigger instance), **read replicas** with read/write split, **caching** (Redis), **connection pooling**, **native table partitioning** on one primary, **CQRS/materialized views**, **archiving cold data**, and moving some workloads to **specialized stores** (e.g. search, analytics). They are preferable when the bottleneck is **reads** not writes, when **data still fits** one primary with headroom, when **strong consistency and simple SQL** matter more than unlimited write scale, or when the organization cannot yet operate multi-shard failure modes safely.

4. What is a "hot shard" problem, and what strategies can mitigate it?

A **hot shard** is one shard receiving disproportionate traffic or data (e.g. a celebrity user or “today’s” time bucket), becoming a bottleneck and SPOF-like despite sharding. Mitigations include **better shard keys** (avoid monotonic-only keys if they skew), **sub-sharding or splitting** hot tenants, **rate limiting and caching** on hot keys, **asynchronous offloading** to queues, **rebalancing** data to new shards, and sometimes **synthetic hashing** or **double hashing** to spread load—combined with monitoring per-shard CPU, lag, and key distribution.

5. How do secondary indexes affect the performance of partitioned databases?

Secondary indexes are typically **created per partition** (or maintained as local structures), so each index is smaller and cheaper to maintain than one global index on a giant heap—but a query that **does not constrain the partition key** may still need to **probe every partition’s index**, multiplying work. Unique constraints must usually **include the partition key** (in systems like PostgreSQL), which shapes schema design. Well-chosen partition keys plus indexes that **prefix the partition key** get the best pruning; “global” secondary access patterns without the partition key can **lose** much of partitioning’s benefit.

## Module 5 — Consistency models, linearizability, consensus, and distributed transactions

### 1. Consistency models

1. What are the trade-offs between strong and weak consistency in distributed systems?

**Strong consistency** (in the sense of “reads reflect latest committed writes” for the chosen scope) simplifies application reasoning and avoids stale decisions, but usually costs **higher latency** (coordination, leader/replica waits), **lower write/read throughput** on the critical path, and **worse availability** under partitions or slow nodes—you may refuse or delay operations rather than serve possibly stale data. **Weak consistency** improves **latency, availability, and horizontal read scale** (e.g. replicas, caches), but clients may see **stale values**, **ordering anomalies**, and **conflicts** that must be handled in the app (merge rules, retries, user messaging, idempotency).

2. What is causal consistency, and how does it differ from eventual consistency?

**Causal consistency** guarantees that if operation B is causally dependent on A (e.g. B reads a value A wrote, or B is issued after observing A’s effect), every node orders A before B consistently; unrelated concurrent operations may still be seen in different orders on different replicas. **Eventual consistency** only promises that if writes stop, replicas **eventually converge** to the same state; it does **not** require preserving causal order between related operations unless the system adds extra mechanisms (vector clocks, explicit causal metadata). So causal is strictly stronger than plain eventual for “related” operations, but weaker than linearizability for all operations globally.

3. Which consistency model ensures that any read returns the result of the most recent write across the system (i.e., no stale data)?

**Linearizability** (single-copy semantics with a real-time global order on overlapping operations) matches the informal “every read sees the latest write” property for individual objects/operations in the usual textbook sense. For **transactions** touching many objects, **strict serializability** (serializable execution that also respects real-time ordering) is the analogous “strongest” database-style guarantee. In practice people also say **strong** or **atomic** consistency when they mean “no stale reads on the path we care about,” but the precise formal answer for “no stale” globally ordered reads/writes is **linearizability** (per object) / **strict serializability** (transactions).

4. Which consistency model provides only eventual convergence of replicas without guaranteeing immediacy of writes on reads?

**Eventual consistency**: replicas may diverge temporarily; given quiescence and delivery of updates, they become equal. Reads **before** convergence may return **any** replica’s value—there is no guarantee that a read immediately after a write observes that write unless extra guarantees (e.g. read-your-writes, quorum reads, or strong consistency) are added.

5. What is the Read-Your-Writes (RYOW) session guarantee, and in what scenarios is it essential?

**Read-your-writes** means that after a client performs a write, its **own subsequent reads** in the same session observe that write (or later updates), even if the store is only **eventually consistent** for other clients. It is essential for **interactive UIs** (save then refresh, multi-step wizards), **after POST redirect**, **session-scoped caches**, and **mobile offline sync** where the user must trust their latest edits before global propagation; it is weaker than full strong consistency because other users may still see old data until replication completes.

### 2. Linearizability and serializability

1. What is the difference between linearizability and serializability?

**Linearizability** is a **correctness condition for concurrent operations on a shared object** (often a register or API): every operation appears to happen at a single instant between its start and end time, consistent with a sequential specification and **real-time order** of non-overlapping ops. **Serializability** is a property of **transactions** over many objects: the outcome is equivalent to **some** serial order of transactions, but that order need **not** match wall-clock time—two transactions can both “appear” to complete before the other in real time as long as their reads/writes don’t forbid a serial reordering. So linearizability is about **single-object atomicity + real-time ordering**; serializability is about **multi-object transactional isolation** without necessarily pinning operations to real-time instants.

### 3. Consensus algorithms

1. What is consensus in distributed systems, and why is it important for high-load applications?

**Consensus** is a group of processes agreeing on **one value** (or a log of commands) despite crashes and **asynchronous networks**, so that decided values are **valid**, **agreed**, and **termination** properties hold under the algorithm’s fault model. High-load systems still need small but critical agreed facts: **leader identity**, **configuration**, **membership**, **work ownership**, **distributed locks**, or **command ordering** in a replicated log—without consensus, split-brain, duplicate processing, or lost updates dominate at scale.

2. What are the main challenges in achieving consensus in a distributed system?

**Network partitions** and **message loss/delay** (FLP: no deterministic consensus in fully async crash-stop model without timeouts); **failures** of nodes during protocol; **performance** (extra rounds, persistence); **operational complexity** (bootstrap, reconfiguration); and in adversarial settings **Byzantine** faults. Practical systems use **timeouts**, **randomization**, or **partial synchrony** assumptions and **leader-based** protocols to make consensus implementable.

3. What is a quorum in distributed consensus, and why is the R+W>N formula important?

A **quorum** is a **minimal subset of replicas** whose participation is required for an operation to be considered successful (e.g. **W** replicas for write, **R** for read out of **N** total). **R + W > N** ensures that any read quorum and any write quorum **intersect in at least one replica**, so a reader can observe a value from a replica that participated in the latest write (under the algorithm’s rules)—tuning R,W trades **read vs write cost** and **staleness vs durability**.

4. What is the Raft consensus algorithm, and why was it developed as an alternative to Paxos?

**Raft** is a **leader-based** replicated log algorithm: **leader election**, **log replication** (AppendEntries), and **safety** rules so committed entries are not lost across terms. It was developed to be **easier to understand and implement correctly** than classical **Paxos**, which is notoriously subtle (multi-Paxos, roles, edge cases) while providing similar crash-fault tolerance for replicated state machines in production systems.

### 4. Distributed transactions

1. What is a distributed transaction, and why do such transactions require special protocols?

A **distributed transaction** is a logical unit of work whose **reads and writes span multiple nodes or services** (multiple databases, partitions, or HTTP participants). A single local **BEGIN/COMMIT** cannot atomically commit everyone: partial failure would leave **some commits and some aborts**, breaking atomicity (A in ACID). Special protocols (**2PC**, **Saga**, **Paxos commit**, etc.) coordinate **all-or-nothing** outcomes or **compensating** effects across participants.

2. How does the Two-Phase Commit (2PC) protocol work to achieve atomic commit across multiple nodes? What's the difference with Three-Phase Commit (3PC) protocol?

**2PC**: Phase 1 **Prepare** — coordinator asks all participants to persist work durably and vote **yes/no**; Phase 2 **Commit/Abort** — if all yes, coordinator sends **commit**; otherwise **abort**. Atomicity holds if participants obey the protocol; the weakness is **blocking** if the coordinator fails after prepare (participants hold locks until recovery). **3PC** adds an extra phase (often **pre-commit** with timeout assumptions) so non-faulty participants can **unblock** without indefinite waiting when the coordinator is suspected dead—reducing blocking in some models but adding **complexity**, **extra latency**, and relying on **stricter timing/network** assumptions; it does not fully solve all failure scenarios in practice, which is why many systems prefer **2PC + recovery log** or **alternatives (Saga)** instead.

3. What is the difference between Choreography-based and Orchestration-based Saga implementations?

**Orchestration**: a central **coordinator** service drives each step (calls A, then B, then issues compensations on failure); easy to trace and change centrally, but the orchestrator is a **dependency and scaling hotspot**. **Choreography**: each service reacts to **domain events** and knows its part of the saga (publish/subscribe); **no central brain**, more decoupled, but **global flow is harder to see**, **ordering** and **duplicate events** need careful design, and debugging crosses many handlers.

4. Why is Correlation ID a good practice in distributed transactions, and how does it help with debugging and tracing?

A **correlation ID** is a **stable identifier** attached to the first inbound request and propagated across **logs, messages, and HTTP headers** for every hop in a saga or call chain. It lets operators **join** scattered log lines into **one story**, measure **end-to-end latency**, find **failed branch** of a partial saga, and tie **metrics/traces** together—essential when atomicity is replaced by **compensation** and many services participate asynchronously.

## Module 6 — Scaling, batch/stream processing, MapReduce, messaging, event sourcing, caching

### 1. Scaling fundamentals

1. What is the difference between vertical and horizontal scaling? What are the trade-offs of each approach?

**Vertical scaling** means making a single node bigger (more CPU, RAM, disk I/O, faster network). It is simpler to operate and keeps data locality on one machine, but you can reach hardware limits, longer maintenance, and a **single point of failure** unless you add redundancy outside that box.

**Horizontal scaling** means adding more nodes to a pool and spreading work across them. It supports **growth** and better **fault tolerance**, but **distributed-system complexity** adds problems with consistency, partitioning, coordination, and stateless or carefully replicated state.

2. What is load balancing, and why is it essential for horizontally scaled systems?

**Load balancing** is distributing incoming work (HTTP requests, TCP connections, gRPC calls, queue consumers, etc.) across multiple healthy backends according to a policy (round-robin, least connections, weighted, geographic, etc.). For horizontal scale it is essential because **clients need one entry point** while **many instances share the load**; without a balancer, traffic would skew to one node, negating scale-out and hurting availability. Balancers also enable **health checks**, **draining**, and **TLS termination**.

3. How does fault tolerance contribute to system reliability in distributed architectures?

**Fault tolerance** means the system continues correct or degraded service when components fail (crashes, slow disks, bad deploys, AZ outages). Techniques include **redundancy**, **replication**, **timeouts and retries with backoff**, **circuit breakers**, **bulkheads**, **idempotent handlers**, and **chaos testing**. Reliability improves because failures are **expected and bounded** instead of cascading; users see fewer outages and data loss when paired with durability and recovery procedures.

### 2. Batch and stream processing

1. What is batch processing, and what are typical use cases where batch processing is most appropriate?

**Batch processing** runs jobs over **finite datasets** on a schedule, optimizing **throughput and cost per byte** over latency. Good fits: nightly **ETL**, **large reports**, **backfills**, and **reconciliation** where minutes-to-hours delay is acceptable.

2. What is stream processing, and how does it differ from batch processing in terms of data handling and latency?

**Stream processing** consumes **continuous, unbounded** event flows and applies transformations **incrementally** as data arrives. Latency is typically **sub-second to low seconds** for useful outputs; state is often **windowed** or **keyed** with **low watermark** semantics. Batch waits for completeness of a slice; streams trade **exact boundary** control for **timeliness** and must handle **late/out-of-order** data explicitly.

3. When would you choose a hybrid processing architecture that combines both batch and stream processing?

Use **hybrid** when you need **fast signals now** and **correct, cheap reconciliation later**: e.g. **real-time fraud scoring** (stream) plus **daily ledger audit** (batch), **live dashboards** (stream aggregates) plus **monthly financial close** (batch), or **Lambda/Kappa** style corrections where a batch job **reprocesses** a window to fix drift. It balances **SLA vs cost** and **approximate vs authoritative** results.

4. What are the main challenges when implementing stream processing in high-load systems?

**Backpressure and skewed keys** (hot partitions), **state size and recovery** (checkpoints, changelog), **exactly-once vs at-least-once** semantics and **idempotency**, **late data** and **watermarks**, **ordering** guarantees across shards, **operational complexity** (versioned jobs, replay), and **joining streams** with **correct time semantics** under load.

### 3. MapReduce and distributed processing

1. What is the MapReduce programming model, and how does it enable distributed data processing?

**MapReduce** is a **data-parallel** pattern: **map** functions run **independently** on splits of input and emit intermediate key-value pairs; a **shuffle** groups all values by key; **reduce** functions aggregate each key’s group. The framework handles **scheduling**, **fault recovery** (re-run failed tasks), and **data locality** (run maps where blocks live), so developers focus on **pure-ish map/reduce logic** while the cluster scales out.

2. What are the Map and Reduce phases, and what happens in each phase?

**Map:** each worker reads an **input split**, applies the user’s **map** function, and writes **intermediate** `(key, value)` records—often to local disk—partitioned by key for the shuffle.

**Reduce:** after shuffle/sort, each **reducer** pulls its partition of keys, iterates sorted values for each key, and runs the user’s **reduce** function to produce **final outputs** (often one row/file per key group).

3. What are some real-world applications that benefit from MapReduce?

**Large-scale log analytics**, **web indexing**, **genomics**, **clickstream aggregation**, **ETL on data lakes**, **training data prep**, and **batch feature generation**—anywhere data is **huge**, **embarrassingly parallel** in the map stage, and needs **group-by-key** aggregation.

### 4. Messaging and event-driven architecture

1. What is the role of a message broker in a distributed system?

A **message broker** (Kafka, RabbitMQ, NATS, etc.) **buffers**, **routes**, and often **persists** messages between producers and consumers. It **decouples** senders and receivers in time and scale, smooths **spikes**, supports **fan-out**, and provides **delivery semantics** (at-most-once, at-least-once, exactly-once with caveats), **ordering** per partition, and **replay** for recovery.

2. What is the difference between the Publisher-Subscriber pattern and the Producer-Consumer pattern?

**Pub/Sub:** publishers emit to **topics** or **exchanges** without knowing subscribers; **many subscribers** can receive the **same** message (broadcast or filtered). **Producer-Consumer:** work is placed in a **queue** and **competing consumers** each take **distinct** messages—one message is usually processed **once** (load-sharing), unless you use competing subscriptions with special rules.

3. What is an event stream, and how is it used in event-driven architectures?

An **event stream** is an **append-only, ordered** sequence of **facts** (domain events) over time, often partitioned for scale. In event-driven architectures, services **publish** streams of what happened; others **subscribe** to react, build **read models**, trigger **integrations**, or drive **sagas**—time becomes a first-class axis of the design.

4. What are the benefits of using event-driven architecture compared to synchronous request-response patterns?

**Looser coupling** and **better resilience** under slow/failing peers (async buffering), **natural scale-out** of consumers, **clearer audit trails** of what occurred, **elasticity** to traffic bursts, and **independent deployment** of handlers—at the cost of **eventual consistency**, **distributed debugging**, and **schema evolution** discipline.

### 5. Event sourcing

1. What is event sourcing, and how does it differ from traditional state management?

**Event sourcing** stores the **sequence of state-changing events** as the **system of record** and derives current state by **replaying** (or folding) them—often with **snapshots** for speed. Traditional state management **overwrites** rows/documents in place; history is secondary or lost unless you add auditing separately.

2. What are the main benefits and challenges of implementing event sourcing?

**Benefits:** **complete audit trail**, **temporal queries** (“as of”), **replay** for new projections or bug fixes, and alignment with **event-driven** integration.

**Challenges:** **schema evolution** on old events, **storage growth**, **performance** of long replays without snapshots, **complexity** in application code, **PII** in immutable logs, and **global ordering** across aggregates.

3. How do event sourcing and CQRS complement each other in distributed systems?

**CQRS** splits **commands** (writes) from **queries** (reads), often with **different models**. Event sourcing fits naturally as the **write side**: append events; **projectors** build **read models** optimized for queries. This keeps the **write path** simple and append-only while **read paths** scale and evolve **independently** (see also the **DB: Replication** module for CQRS details).

### 6. Advanced patterns

1. Why is immutability important in event-driven systems, especially with respect to state management?

**Immutable events** are a **stable, auditable** source of truth: consumers can **replay**, **rebuild projections**, and **detect duplicates** deterministically. Mutating stored events in place breaks **ordering**, **replay**, and **multi-consumer** assumptions; immutability pairs with **compensating events** instead of silent edits, which preserves **causal history** and simplifies **recovery**.

### 7. Caching and performance

1. What are common distributed caching patterns (cache-aside, write-through, write-behind)?

**Cache-aside:** app reads cache first, on miss loads from DB and **populates** cache; writes update DB and **invalidate or update** cache explicitly—simple, but risk of **stale** data if invalidation is wrong.

**Write-through:** writes go **through** the cache to the backing store synchronously—**stronger consistency** between cache and DB, **higher write latency**.

**Write-behind (write-back):** writes hit **cache first** and **async flush** to DB—**low write latency** and **burst absorption**, but **durability risk** until flush and **complex failure** handling.

2. How does distributed caching help address scalability bottlenecks?

Shared caches (e.g. **Redis cluster**) offload **hot reads** from databases, reduce **connection** and **CPU** pressure on primaries, and keep **session or computed** state near compute. **Horizontal cache shards** replicate the “cheap read” tier so application instances stay **stateless** while perceived **latency** drops.

3. What factors should you consider when setting cache expiration (TTL) policies?

**Data freshness** requirements, **cost** of stale reads vs DB load, **key churn** and **memory** pressure, **negative caching** for misses, **jitter** to avoid **thundering herds**, **legal retention** for sensitive values, and whether **event-driven invalidation** can replace or shorten TTLs for critical keys.

### 8. Practical application

1. For a job scheduling system, would you use batch processing, stream processing, or a hybrid approach for executing scheduled jobs? Justify your choice.

**Hybrid (or stream-primary with batch maintenance)** is usually best: **stream or queue-driven workers** fire jobs at their **due times** with low latency and scale with **consumer groups**; **batch** jobs handle **reconciliation**, **backfills**, **metrics rollups**, and **data repair** after outages. Pure batch is too coarse for “run at 09:00:00”; pure infinite stream still benefits from **periodic** correctness passes.

2. What events would you publish for the use case "Create a New Job"? Design a sample event schema.

Publish **domain events** that reflect facts, not internal DTO noise, e.g. `JobCreated` after persistence. Sample **JSON** schema (illustrative):

```json
{
  "eventType": "JobCreated",
  "eventVersion": 1,
  "occurredAt": "2026-06-08T12:00:00.000Z",
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "tenantId": "acme",
  "schedule": {
    "kind": "cron",
    "expression": "0 9 * * MON-FRI",
    "timeZone": "Europe/Moscow"
  },
  "definitionRef": "urn:jobdef:reports:daily-sales",
  "createdBy": "user:42",
  "correlationId": "7b2c3d4e-8f90-1a2b-3c4d-5e6f708192a0"
}
```

You might also emit **`JobCreationRequested`** before commit and **`JobCreationFailed`** on validation errors if other services must react.

3. How would you handle failure scenarios in an event-driven architecture (e.g., message broker downtime, consumer failures)?

**Broker downtime:** buffer on producer side if possible, **idempotent** publishing, **outbox pattern** with DB so messages **survive** restarts; degrade gracefully with **circuit breakers** and **alerting**. **Consumer failures:** **at-least-once** processing with **idempotent handlers**, **retry** with backoff, **DLQ** for poison messages, **rebalancing** partitions on crash, **health checks** and **auto-restart**, and **replay** from durable log offsets after fixes.

4. If you need to process 1 million jobs scheduled for execution at the same time, what processing model and architecture would you choose?

Use a **durable queue or partitioned event log** (Kafka/SQS/etc.) plus **many stateless workers** behind **autoscaling** (Kubernetes, serverless with concurrency limits), **shard by partition key** (tenant or job type) to avoid hot keys, **rate limits** to downstream systems, **idempotency keys**, **scheduler** that enqueues **fire times** rather than running 1M timers in one process, and **backpressure**—optionally a **two-tier** model: quick **acceptance** path and **throttled execution** to protect dependencies. **Batch** analytics afterward for **SLA reporting**.
