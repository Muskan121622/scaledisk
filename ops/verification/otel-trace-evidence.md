# Stage 5B - OpenTelemetry distributed trace (live)

Stack: API on host :5102; Postgres, Kafka (apache/kafka:3.8.1), Jaeger
(jaegertracing/all-in-one, OTLP gRPC :4317, UI :16686) as containers.

Single traceId - also returned to the client as the X-Correlation-Id header:
  205b86aeb40a755aaee0adef76e395d6

Spans in causal order (9 total, ONE trace id):
 1 HTTP  POST api/tickets        - originating request
 2 DB    SELECT customers        - tenant-scoped customer validation
 3 DB    SELECT sla_policies     - SLA selection
 4 DB    INSERT outbox_messages  - event + TraceParent snapshot (ScaleDeskDbContext)
 5 MSG   outbox publish          - background drain -> Kafka (child of the request trace)
 6 MSG   kafka consume           - consumer, linked via W3C traceparent across the broker
 7 DB    SELECT EXISTS processed_messages - idempotency dedup check
 8 DB    SELECT automation_rules          - automation engine handlers
 9 DB    INSERT processed_messages         - idempotency marker

Proof: HTTP -> Postgres -> Outbox -> Kafka -> consumer -> automation/notification all
share ONE trace id. The async hop works because the outbox row persists the request's
W3C traceparent (ScaleDeskDbContext.AddOutboxMessages), the publisher re-injects it as a
Kafka header (KafkaEventPublisher), and the consumer restores it as the parent context
(KafkaEventConsumer). CorrelationId == Activity.TraceId (RequestContextMiddleware).

View: docker compose up -d jaeger, open http://localhost:16686, or
  GET http://localhost:16686/api/traces/<traceId>   (response is wrapped in {"data":[...]})
Re-run:  ops\verification\otel-trace.ps1  (login -> create ticket -> assert linked trace)

Note: the outbox dispatcher polls every second; each poll emits an EF span and can flood
Jaeger's in-memory store. Raise Kafka__PollIntervalMilliseconds during load tests.
