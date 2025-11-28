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
