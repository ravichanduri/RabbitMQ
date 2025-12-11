# Flipkart — RabbitMQ Demo (Publisher + Consumer)

A small, production-ish demo showing how to build an event-driven order pipeline in C#:

- **Flipkart.Publisher** — ASP.NET Core Web API (publishes order events to RabbitMQ)  
- **Flipkart.Consumer** — .NET Worker Service (consumes events, processes orders)
- RabbitMQ broker for message routing, durability, DLQ and back-pressure

This repo is intended as a learning / portfolio project demonstrating secure, durable messaging patterns (durable exchange/queues, manual ACKs, DLQ, prefetch), how to run locally (Visual Studio or Docker) and how to test the end-to-end flow.

---

## ⭐ How to Start RabbitMQ Locally

### **Option 1 — Run RabbitMQ using Docker (recommended)**

```bash
docker run -d   --name flipkart_rabbitmq   -p 5672:5672   -p 15672:15672   --hostname flipkart-rabbit   -e RABBITMQ_DEFAULT_USER=appuser   -e RABBITMQ_DEFAULT_PASS=SuperStrongPassword123!   -e RABBITMQ_DEFAULT_VHOST=app_vhost   rabbitmq:3.13-management
```

RabbitMQ will be available at:

- **AMQP:** `amqp://localhost:5672`
- **Management UI:** http://localhost:15672  
  Username: `appuser`  
  Password: `SuperStrongPassword123!`

---

### **Option 2 — Start via Docker Compose**

```bash
docker compose up -d rabbitmq
```

(Requires `docker-compose.yml` including a `rabbitmq` service.)

---

## Table of contents

- Features  
- Prerequisites  
- Repository structure  
- Configuration  
- Run locally (Visual Studio)  
- Run with Docker Compose  
- Testing the flow  
- Scaling & load testing  
- Debugging tips  
- FAQ / Troubleshooting  
- Next steps / Improvements  
- License

---

## Features

- Publisher API that accepts order requests and publishes `OrderCreated` messages to RabbitMQ.
- Consumer worker that:
  - Declares exchange/queue/DLQ at startup (idempotent)
  - Uses manual ACKs and `BasicQos` prefetch to control load
  - Sends poison/failing messages to Dead Letter Queue
- Configurable via `appsettings.json` or environment variables
- Docker-ready (`Dockerfile` for each project + `docker-compose.yml` sample)
- Small, easy-to-extend codebase.

---

## Prerequisites

- .NET SDK 8.0  
- Visual Studio 2022 or VS Code  
- Docker Desktop (optional)  
- RabbitMQ instance (local or docker)

RabbitMQ Management UI: `http://localhost:15672`  
Ports: AMQP `5672`, Management `15672`

---

## Repository structure

```
/Flipkart
  /Flipkart.Publisher
  /Flipkart.Consumer
  docker-compose.yml
  README.md
```

---

## Configuration

Example `appsettings.json`:

```
"RabbitMq": {
  "HostName": "localhost",
  "Port": 5672,
  "UserName": "appuser",
  "Password": "SuperStrongPassword123!",
  "VirtualHost": "app_vhost",
  "ExchangeName": "flipkart.orders.exchange",
  "QueueName": "orders.queue",
  "RoutingKey": "orders.created",
  "DeadLetterExchange": "orders.dlx",
  "DeadLetterQueue": "orders.dlq"
}
```

---

## Run locally (Visual Studio)

1. Start RabbitMQ using the Docker command above.  
2. Open `Flipkart.sln`.  
3. Set **Multiple Startup Projects** → Publisher + Consumer.  
4. Run → Swagger opens → POST `/api/FlipkartOrders`.

---

## Docker Compose

```
docker compose build
docker compose up -d
```

RabbitMQ UI: http://localhost:15672

Scale consumers:
```
docker compose up -d --scale flipkart_consumer=3
```

---

## Testing

Swagger:
```
POST /api/FlipkartOrders
{
  "amount": 199.99
}
```

Consumer logs show message processing.

Check queues in RabbitMQ UI.

---

## FAQ

- `ACCESS_REFUSED`: fix user/password/vhost.  
- Messages stuck in queue: check consumer logs + ACK behavior.  
- DLQ not working: ensure `x-dead-letter-exchange` is set.

---

## License

MIT
