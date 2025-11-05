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