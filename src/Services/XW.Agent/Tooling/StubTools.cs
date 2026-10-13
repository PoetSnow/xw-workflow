using System.Diagnostics;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Tooling;

/// <summary>产品检索 stub（Host 不可用时 FallbackToStub）。</summary>
public sealed class StubProductsSearchTool : IAgentTool
{
    public string Name => "products.search";
    public string Description => "stub：按 keywords 模拟产品列表";
    public string RequiredPermission => AgentPermissionCodes.InventoryRead;
    public IReadOnlyList<string> RequiredSlots => ["keywords"];

    public Task<ToolExecutionResult> ExecuteAsync(
        AgentSession session, IntentResult intent, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var kw = intent.Slots.GetValueOrDefault("keywords")
                 ?? session.Slots.GetValueOrDefault("keywords")
                 ?? "DEMO";
        var id = 10000 + Math.Abs(kw.GetHashCode(StringComparison.OrdinalIgnoreCase)) % 90000;
        sw.Stop();
        return Task.FromResult(new ToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            ElapsedMs = sw.ElapsedMilliseconds,
            Summary = $"products.search：共 1 条。id={id} code={kw} name=演示产品-{kw}",
            Data = new { data = new[] { new { id, code = kw, name = $"演示产品-{kw}" } }, totalCount = 1, source = "stub" }
        });
    }
}

/// <summary>物料检索 stub。</summary>
public sealed class StubMaterialsSearchTool : IAgentTool
{
    public string Name => "materials.search";
    public string Description => "stub：按 keywords 模拟物料列表";
    public string RequiredPermission => AgentPermissionCodes.InventoryRead;
    public IReadOnlyList<string> RequiredSlots => ["keywords"];

    public Task<ToolExecutionResult> ExecuteAsync(
        AgentSession session, IntentResult intent, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var kw = intent.Slots.GetValueOrDefault("keywords")
                 ?? session.Slots.GetValueOrDefault("keywords")
                 ?? "DEMO";
        var id = 20000 + Math.Abs(kw.GetHashCode(StringComparison.OrdinalIgnoreCase)) % 80000;
        sw.Stop();
        return Task.FromResult(new ToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            ElapsedMs = sw.ElapsedMilliseconds,
            Summary = $"materials.search：共 1 条。id={id} code={kw} name=演示物料-{kw}",
            Data = new { data = new[] { new { id, code = kw, name = $"演示物料-{kw}" } }, totalCount = 1, source = "stub" }
        });
    }
}

/// <summary>库存账 stub。</summary>
public sealed class StubInventoryBalancesTool : IAgentTool
{
    public string Name => "inventory.balances.query";
    public string Description => "stub：按 productId/materialId 模拟库存";
    public string RequiredPermission => AgentPermissionCodes.InventoryRead;
    public IReadOnlyList<string> RequiredSlots => [];

    public Task<ToolExecutionResult> ExecuteAsync(
        AgentSession session, IntentResult intent, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var productId = intent.Slots.GetValueOrDefault("productId")
                        ?? session.Slots.GetValueOrDefault("productId");
        var materialId = intent.Slots.GetValueOrDefault("materialId")
                         ?? session.Slots.GetValueOrDefault("materialId");
        var key = productId ?? materialId ?? "0";
        var seed = Math.Abs(key.GetHashCode(StringComparison.Ordinal));
        var onHand = 50 + seed % 200;
        var reserved = seed % 40;
        sw.Stop();
        return Task.FromResult(new ToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            ElapsedMs = sw.ElapsedMilliseconds,
            Summary =
                $"inventory.balances.query：共 1 条。仓1 现存{onHand} 预留{reserved}（productId={productId ?? "-"} materialId={materialId ?? "-"}）",
            Data = new
            {
                data = new[]
                {
                    new { warehouseId = 1, qtyOnHand = onHand, qtyReserved = reserved, productId, materialId }
                },
                totalCount = 1,
                source = "stub"
            }
        });
    }
}

/// <summary>订单履约查询 stub（Host 未绑定时演示用）。</summary>
public sealed class StubOrderFulfillmentQueryTool : IAgentTool
{
    public string Name => "order.fulfillment.query";
    public string Description => "stub：按订单号模拟履约状态";
    public string RequiredPermission => AgentPermissionCodes.OrderRead;
    public IReadOnlyList<string> RequiredSlots => ["orderNo"];

    public Task<ToolExecutionResult> ExecuteAsync(AgentSession session, IntentResult intent, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var orderNo = intent.Slots.GetValueOrDefault("orderNo")
                      ?? session.Slots.GetValueOrDefault("orderNo")
                      ?? "UNKNOWN";
        var statuses = new[] { "待审核", "待发货", "已发货", "运输中", "已签收" };
        var status = statuses[Math.Abs(orderNo.GetHashCode(StringComparison.Ordinal)) % statuses.Length];
        sw.Stop();

        return Task.FromResult(new ToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            ElapsedMs = sw.ElapsedMilliseconds,
            Summary = $"订单 {orderNo} 当前履约状态：{status}",
            Data = new { orderNo, status, source = "stub" }
        });
    }
}

/// <summary>低库存预警 stub。</summary>
public sealed class StubLowStockAlertTool : IAgentTool
{
    public string Name => "alert.low_stock.list";
    public string Description => "stub：模拟低库存列表";
    public string RequiredPermission => AgentPermissionCodes.AlertRead;
    public IReadOnlyList<string> RequiredSlots => [];

    public Task<ToolExecutionResult> ExecuteAsync(AgentSession session, IntentResult intent, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var items = new[]
        {
            new { materialId = "1001", warehouseId = "1", available = 3, threshold = 10 },
            new { materialId = "2088", warehouseId = "2", available = 1, threshold = 5 }
        };
        sw.Stop();

        return Task.FromResult(new ToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            ElapsedMs = sw.ElapsedMilliseconds,
            Summary = $"当前低库存预警 {items.Length} 条：物料1001(仓1剩3)、物料2088(仓2剩1)",
            Data = new { items, source = "stub" }
        });
    }
}
