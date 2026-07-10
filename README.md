# Gateway — YARP API 网关

基于 YARP (Yet Another Reverse Proxy) 的 API 网关，作为整个系统的统一入口，负责将外部请求路由到后端的 Shop、WMS 等微服务。

## 技术栈

- **框架**: ASP.NET Core 8 + Yarp.ReverseProxy
- **数据库**: 无

## 路由配置

路由规则定义在 `appsettings.json` 的 `ReverseProxy` 节：

| 路由前缀 | 目标服务 | 说明 |
|---|---|---|
| `/api/orders/*` | `http://shop-api:8080` | 订单相关请求转发至 Shop |
| `/api/inventory/*` | `http://wms-api:8080` | 库存相关请求转发至 WMS |

## 端口

| 端口 | 说明 |
|---|---|
| 5000 | 外部访问端口 |

所有外部请求统一通过 `http://localhost:5000/` 进入，由 Gateway 转发到对应微服务。

## 本地运行

```bash
dotnet run --project gateway/Gateway.csproj
```

> 单个 API 调试时可直接访问对应微服务的端口（5001/5002/5003），绕过网关。
