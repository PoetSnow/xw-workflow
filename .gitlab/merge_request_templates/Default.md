## 改动说明

- **目标：**
- **范围：** （Host / Devices / Integration / Brain / Customer / Console / SQL / 文档）

## 自测

- [ ] 本地 `dotnet build` 通过
- [ ] 涉及 Console 时前端可打开
- [ ] 本 MR 的 GitLab Pipeline 已通过

## 审查清单

- [ ] 跨模块未引用对方 Domain（仅 Contracts；CI `architecture_gate` 已检查）
- [ ] Integration / Brain 未连接业务库
- [ ] 无密钥进入仓库
- [ ] 行为变更已同步文档/SQL（若适用）

## 发布

- 合入 `main`/`master` 后自动 **deploy_test**（150）
- **deploy_prod** 需在 Pipeline 里手动点击
