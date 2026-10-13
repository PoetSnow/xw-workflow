# Skill 目录

按业务域分子目录，**一个 JSON 文件 = 一个接口/一条 Skill**。保存后 Agent 热加载，不必改 C#、不必把能力堆进 `appsettings.json`。

```
skills/
  inventory/     产品检索、物料、库存账、查库存（内部 workflow）
  purchase/      供应商、比价、创建采购草稿
  knowledge/     知识库检索（只需这一条）
  order/         订单履约
  alert/         低库存预警
```

新增：复制一个现有文件，改 `Code` / `Http.Path` / 权限，放到对应域目录。`GET /api/agent/capabilities` 可确认已加载。
