# Gateway — YARP API 网关

基于 **YARP (Yet Another Reverse Proxy)** 构建的 API 网关，作为整个微服务系统的**统一流量入口**，负责请求路由、JWT 身份认证和 Redis 限流。

## 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 8.0 | 运行时框架 |
| Yarp.ReverseProxy | 2.3.0 | 反向代理核心 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.0 | JWT 认证中间件 |
| StackExchange.Redis | 2.8.16 | Redis 客户端（限流） |
| Serilog.AspNetCore | 8.0.2 | 结构化日志 |
| Swashbuckle.AspNetCore | 6.5.0 | Swagger 文档 |

## 功能特性

### 反向代理路由

| 外部路径 | 目标服务 | 后端地址 | 鉴权 |
|----------|----------|----------|------|
| `/api/auth/*` | Identity.API (认证服务) | `http://identity-api:8080` | 无（公开） |
| `/api/orders/*` | Shop.API (订单服务) | `http://shop-api:8080` | 需要 JWT |
| `/api/inventory/*` | WMS.API (仓储服务) | `http://wms-api:8080` | 需要 JWT |
| `/api/payments/*` | Payment.API (支付服务) | `http://payment-api:8080` | 需要 JWT |

> `/api/auth/*` 路由未配置 `AuthorizationPolicy`，允许匿名访问；其余业务路由配置 `"AuthorizationPolicy": "default"`，要求请求携带有效 JWT。

### JWT 认证

- **认证方案**: JWT Bearer (`JwtBearerDefaults.AuthenticationScheme`)
- **签名算法**: HMAC-SHA256 对称密钥
- **验证项**: Issuer + Audience + Lifetime + SigningKey 全部启用
- **配置来源**: `appsettings.json` → `Jwt` 节，Docker 环境通过环境变量 `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` 注入
- **Token 签发方**: Identity.API（Gateway 仅做验证，不签发 Token）

### Redis 分布式限流

基于 **Redis 有序集合 (Sorted Set)** 实现的**滑动窗口限流**：

- **粒度**: 按客户端 IP 独立计数
- **默认限制**: 每个 IP 每 10 秒最多 100 次请求
- **超额响应**: HTTP 429 Too Many Requests，返回 JSON 提示
- **配置**: 通过 `appsettings.json` 的 `RateLimit` 节动态调整

```json
"RateLimit": {
  "PermitLimit": 100,
  "WindowSeconds": 10
}
```

### 结构化日志

基于 **Serilog** 的日志系统：

- **控制台**: 实时输出，带颜色高亮
- **文件**: 按天滚动，保留在 `logs/gateway-{yyyyMMdd}.log`
- **标签**: 每条日志附带 `Service: Gateway` 属性，便于日志聚合

## 架构设计

```
Client (前端/第三方)
    │
    ▼
┌──────────────────────────────┐
│    Gateway (端口 5000)        │
│  ┌────────────────────────┐  │
│  │  Redis 滑动窗口限流      │  │
│  └────────────────────────┘  │
│  ┌────────────────────────┐  │
│  │  JWT Bearer 认证中间件   │  │
│  └────────────────────────┘  │
│  ┌────────────────────────┐  │
│  │  YARP 反向代理引擎       │  │
│  └────────────────────────┘  │
└──┬───────┬───────┬───────┬──┘
   │       │       │       │
   ▼       ▼       ▼       ▼
Identity  Shop    WMS   Payment
(5005)   (5001)  (5002)  (5004)
```

## 端口映射

| 端口 | 服务 | 说明 |
|------|------|------|
| 5000 | Gateway | 网关统一入口 |
| 5001 | Shop.API | 订单服务（直连） |
| 5002 | WMS.API | 仓储服务（直连） |
| 5003 | Seller.API | 商家通知服务（直连，纯 MQ 消费者） |
| 5004 | Payment.API | 支付服务（直连） |
| 5005 | Identity.API | 认证服务（直连） |

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

## 依赖服务

| 依赖 | 用途 |
|------|------|
| Redis | 滑动窗口限流的数据存储 |
| Identity.API | JWT Token 签发（密钥需一致） |
| Shop.API | 订单业务处理 |
| WMS.API | 库存仓储业务处理 |
| Payment.API | 支付业务处理 |