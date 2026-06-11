# Comparison Report: JSON vs Protobuf for UC 2.3 History API

## 1. Reference baseline (pre-built lab tool)


### Payload size

| Records | JSON (bytes) | Protobuf (bytes) | JSON vs proto (ratio) | Size reduction vs JSON |
|---------|-------------:|-----------------:|----------------------:|------------------------:|
| 100     | 15,005       | 4,460            | 3.4×                  | **70.3%** |
| 500     | 75,544       | 22,727           | 3.3×                  | **69.9%** |
| 1,000   | 151,207      | 45,596           | 3.3×                  | **69.8%** |

**Average size reduction (proto vs JSON): ~70%** for this simple record shape.

### Serialization performance (ms/op, mean of 20 runs)

| Records | JSON serialize | Proto serialize | JSON deserialize | Proto deserialize |
|--------:|---------------:|----------------:|-----------------:|------------------:|
| 100     | 0.05           | 0.05            | 0.41             | 0.04              |
| 500     | 0.68           | 0.40            | 2.53             | 0.28              |
| 1,000   | 0.95           | 0.35            | 4.46             | 0.27              |

Proto deserialize is **~17× faster** at 1,000 rows vs JSON in this run; serialize is **~2.7× faster** for proto at 1,000 rows.

---

## 2. JobScheduler implementation (UC 2.3 wire models)

### Payload size

| Records | JSON (bytes) | Protobuf (bytes) | JSON vs proto (ratio) | Size reduction vs JSON |
|---------|-------------:|-----------------:|----------------------:|------------------------:|
| 100     | 23,427       | 15,555           | 1.5×                  | **33.6%** |
| 500     | 117,367      | 78,055           | 1.5×                  | **33.5%** |
| 1,000   | 234,793      | 156,181          | 1.5×                  | **33.5%** |

**Average size reduction (proto vs JSON): ~33.5%** for the richer UC 2.3 row shape.

### Serialization performance (ms/op, mean of 20 runs)

| Records | JSON serialize | Proto serialize | JSON deserialize | Proto deserialize |
|--------:|---------------:|----------------:|-----------------:|------------------:|
| 100     | 0.23           | 0.07            | 0.80             | 0.06              |
| 500     | 1.86           | 0.53            | 4.81             | 0.46              |
| 1,000   | 2.60           | 1.68            | 7.48             | 2.04              |

At **1,000 rows**, Protobuf **serializes ~1.5× faster** and **deserializes ~3.7× faster** than JSON in this run (both payloads larger than the minimal lab list, so times are higher than the first table).

---

## 3. Bandwidth impact at scale (1,000 concurrent downloaders)

Assumption: each client downloads **1,000 records** per response, **once per second**, all overlapping (worst-case aggregate egress).

| Scenario | Payload / response | Aggregate egress (1000 clients × 1 req/s) |
|----------|-------------------:|--------------------------------------------:|
| **Baseline** JSON | 151,207 B (~147.7 KiB) | ~144.2 MB/s |
| **Baseline** Protobuf | 45,596 B (~44.5 KiB) | ~43.5 MB/s |
| **Baseline savings** | | **~100.7 MB/s less (~69.8%)** |
| **JobScheduler** JSON | 234,793 B (~229.3 KiB) | ~224.0 MB/s |
| **JobScheduler** Protobuf | 156,181 B (~152.5 KiB) | ~149.0 MB/s |
| **JobScheduler savings** | | **~75.0 MB/s less (~33.5%)** |

---

## 4. Conclusion (summary)

The **pre-built lab baseline** shows protobuf-net roughly **3.3× smaller** than camelCase JSON and **much faster deserialization** on a compact `JobHistoryRecord` list—useful as an upper bound on what binary formats can achieve on a small schema.

The **JobScheduler UC 2.3** payloads are richer; **Google.Protobuf** still cuts wire size by **~33.5%** versus compact JSON and cuts **serialize/deserialize CPU** materially at 500–1,000 events, which helps under **many concurrent downloaders** (less bandwidth and less GC pressure on hot paths). **JSON** remains appropriate for debugging, browsers, and contract-free clients; **Protobuf (gRPC)** is the better default for high-concurrency internal consumers that already share the `.proto`.

---

## 5. Production format decision

**Chosen for production (primary path): Protobuf over gRPC** (`GetExecutionHistoryPage` / `GetExecutionHistoryPageResponse`), with **JSON** kept as the **baseline public/internal REST** surface where human inspection and universal clients matter.

| Criterion | Concrete numbers (this report) | Why Protobuf wins production |
|-----------|-------------------------------|------------------------------|
| **Payload at 1,000 events** | JSON **234,793 B** vs proto **156,181 B** (§2) | **78,612 fewer bytes per response** (~33.5% reduction)—directly lowers CDN/API egress and tail latency when fanning out to many downloaders. |
| **Worst-case aggregate egress** | **~224 MB/s** JSON vs **~149 MB/s** proto for 1K clients × 1K rows × 1 req/s (§3) | **~75 MB/s less** sustained load on the same scenario—fewer bits on the wire and less buffer memory under concurrency. |
| **Deserialize at 1,000 rows** | JSON **7.48 ms/op** vs proto **2.04 ms/op** (§2) | **~3.7× less CPU time per response** on the hot read path—critical when many workers deserialize in parallel (lower p99, less GC). |
| **Serialize at 1,000 rows** | JSON **2.60 ms/op** vs proto **1.68 ms/op** (§2) | **~1.5× faster** server-side encoding for large pages. |
| **Reference ceiling (simple schema)** | Lab: JSON **151,207 B** vs proto **45,596 B** at 1,000 rows; deserialize **4.46 ms** vs **0.27 ms** (§1) | Shows binary formats can dominate even harder on slimmer models; our real UC 2.3 shape is heavier but **same directional benefit**. |

**Where JSON stays in production:** operator tooling, Swagger/OpenAPI, emergency `curl`, and any client that cannot adopt gRPC or the shared `.proto`. That is a **complementary** channel, not the throughput-optimized default for 1,000 concurrent downloaders described in the UC constraint.
