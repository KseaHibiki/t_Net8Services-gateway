# Gateway — YARP API 网关

基于 YARP (Yet Another Reverse Proxy) 的 API 网关，作为整个系统的统一入口，负责将外部请求路由到后端的 Shop、WMS 等微服务。

## 技术栈

- **框架**: ASP.NET Core 8 + Yarp.ReverseProxy
- **日志**: Serilog（控制台 + 按天滚动文件）
- **数据库**: 无

## 路由规则

| 外部路径 | 目标服务 | 后端地址 | 说明 |
|---|---|---|---|
| `/api/orders/*` | Shop.API | `http://shop-api:8080` | 创建订单、查询订单 |
| `/api/inventory/*` | WMS.API | `http://wms-api:8080` | 初始化库存、查询库存 |

## 通过网关调用的接口

### POST /api/orders — 创建订单（转发至 Shop）

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"productId":"550e8400-e29b-41d4-a716-446655440000","quantity":5}'
```

响应与 Shop.API 直连完全一致。

### GET /api/orders/{id} — 查询订单（转发至 Shop）

```bash
curl http://localhost:5000/api/orders/c4b4463f-1a2b-3c4d-5e6f-7a8b9c0d1e2f
```

### POST /api/inventory/seed — 初始化库存（转发至 WMS）

```bash
curl -X POST http://localhost:5000/api/inventory/seed \
  -H "Content-Type: application/json" \
  -d '{"productId":"550e8400-e29b-41d4-a716-446655440000","quantity":100}'
```

### GET /api/inventory/{productId} — 查询库存（转发至 WMS）

```bash
curl http://localhost:5000/api/inventory/550e8400-e29b-41d4-a716-446655440000
```

## 端口

| 端口 | 说明 |
|---|---|
| 5000 | 网关外部访问端口 |

## 直连 vs 网关

| 方式 | 地址 | 适用场景 |
|---|---|---|
| 通过网关 | `http://localhost:5000/api/...` | 生产环境、前端调用 |
| 直连服务 | `http://localhost:5001/api/...` | 本地调试单个服务 |

## 本地运行

```bash
dotnet run --project gateway/Gateway.csproj
```