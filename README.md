# Gateway — YARP API 网关

基于 **YARP (Yet Another Reverse Proxy)** 构建的 API 网关，作为整个微服务系统的**统一流量入口**，负责将外部请求路由到后端的 Shop、WMS、Payment 等微服务。

## 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 8.0 | 运行时框架 |
| Yarp.ReverseProxy | 2.3.0 | 反向代理核心 |
| StackExchange.Redis | 2.8.16 | Redis 客户端（限流） |
| Serilog.AspNetCore | 8.0.2 | 结构化日志 |
| Swashbuckle.AspNetCore | 6.5.0 | Swagger 文档 |

## 功能特性

### 反向代理路由

| 外部路径 | 目标服务 | 后端地址 |
|----------|----------|----------|
| `/api/orders/*` | Shop.API (订单服务) | `http://shop-api:8080` |
| `/api/inventory/*` | WMS.API (仓储服务) | `http://wms-api:8080` |
| `/api/payments/*` | Payment.API (支付服务) | `http://payment-api:8080` |

### Redis 分布式限流

基于 **Redis 有序集合 (Sorted Set)** 实现的**滑动窗口限流**：

- **粒度**：按客户端 IP 独立计数
- **默认限制**：每个 IP 每 10 秒最多 100 次请求
- **超额响应**：HTTP 429 Too Many Requests，返回 JSON 提示
- **配置**：通过 `appsettings.json` 的 `RateLimit` 节动态调整

```json
"RateLimit": {
  "PermitLimit": 100,
  "WindowSeconds": 10
}
```

### 结构化日志

基于 **Serilog** 的日志系统，输出格式统一：

- **控制台**：实时输出，带颜色高亮
- **文件**：按天滚动，保留在 `logs/gateway-{yyyyMMdd}.log`
- **标签**：每条日志附带 `Service: Gateway` 属性，便于日志聚合

### Swagger 文档

开发环境下自动启用 Swagger UI，方便调试和测试。

## 架构设计

```
Client (前端/第三方)
    │
    ▼
┌──────────────────────┐
│    Gateway (端口 5000)  │
│  ┌────────────────┐   │
│  │  Redis 限流中间件  │   │
│  └────────────────┘   │
│  ┌─────────────────┐  │
│  │ YARP 反向代理引擎  │  │
│  └─────────────────┘  │
└──────┬──────┬──────┬──┘
       │      │      │
       ▼      ▼      ▼
  Shop.API  WMS.API  Payment.API
  (5001)    (5002)   (5004)
```

## API 使用示例

### 创建订单

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"productId":"550e8400-e29b-41d4-a716-446655440000","quantity":5}'
```

### 查询订单

```bash
curl http://localhost:5000/api/orders/{orderId}
```

### 初始化库存

```bash
curl -X POST http://localhost:5000/api/inventory/seed \
  -H "Content-Type: application/json" \
  -d '{"productId":"550e8400-e29b-41d4-a716-446655440000","quantity":100}'
```

### 查询库存

```bash
curl http://localhost:5000/api/inventory/{productId}
```

## 端口映射

| 端口 | 服务 | 说明 |
|------|------|------|
| 5000 | Gateway | 网关统一入口 |
| 5001 | Shop.API | 订单服务（直连） |
| 5002 | WMS.API | 仓储服务（直连） |
| 5004 | Payment.API | 支付服务（直连） |

## 环境配置

服务通过 `appsettings.json` + `appsettings.{Environment}.json` 实现多环境配置，由 `ASPNETCORE_ENVIRONMENT` 环境变量控制。

| 配置文件 | 适用环境 | 日志级别 | Redis 连接 | 限流（每 IP 每 10s） |
|----------|:--------:|:--------:|:----------:|:-------------------:|
| `appsettings.json` | 基础公共 | — | `localhost:6379` | 100 次 |
| `appsettings.Development.json` | Development | Debug | `localhost:6379` | 200 次 |
| `appsettings.Production.json` | Production | Information | Docker 内部 (环境变量覆写) | 100 次 |

> `docker-compose.yml` 中设置 `ASPNETCORE_ENVIRONMENT=Development`，`docker-compose.prod.yml` 中设置为 `Production`。

## 本地运行

```bash
# Development 模式（默认）
dotnet run --project gateway/Gateway.csproj

# 或指定环境
ASPNETCORE_ENVIRONMENT=Development dotnet run --project gateway/Gateway.csproj
```

Docker Compose 方式（推荐）：

```bash
docker-compose up -d gateway
```

## CI/CD

[Jenkinsfile](file:///d:/Net8Service/t_Net8Services/gateway/Jenkinsfile) 配置了完整的 CI 流水线：

1. **Checkout** — 从 GitHub 拉取代码
2. **Restore & Build** — 还原依赖并编译 Release
3. **Docker Build & Push** — 构建镜像并推送到本地仓库 `localhost:5005`

## 依赖服务

| 依赖 | 用途 |
|------|------|
| Redis | 滑动窗口限流的数据存储 |
| Shop.API | 订单业务处理 |
| WMS.API | 库存仓储业务处理 |
| Payment.API | 支付业务处理 |
