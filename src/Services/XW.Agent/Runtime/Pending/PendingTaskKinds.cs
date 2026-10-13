namespace XW.Agent.Application.Runtime.Pending;

/// <summary>待办 kind / step / 槽位常量。</summary>
public static class PendingTaskKinds
{
    public const string SlotKind = "_pending.kind";
    public const string SlotStep = "_pending.step";
    public const string SlotIdempotencyKey = "_pending.idempotencyKey";
    public const string SlotLineIdempotencyKey = "_pending.lineIdempotencyKey";
    public const string SlotSupplierCandidates = "_pending.supplier_candidates";

    public const string KindPurchaseCreate = "purchase_create";

    /// <summary>库存结论后：等用户同意进入采购。</summary>
    public const string StepAwaitConfirm = "await_confirm";

    /// <summary>比价后：等用户选供应商/补单价。</summary>
    public const string StepAwaitSupplierPrice = "await_supplier_price";

    /// <summary>槽位齐：等用户最终确认创建草稿（与补槽分离）。</summary>
    public const string StepAwaitFinalConfirm = "await_final_confirm";
}

/// <summary>待办语境下的用户回复分类。</summary>
public enum PendingTaskReplyKind
{
    Unrelated = 0,
    Affirm = 1,
    Cancel = 2,
    ProvideSlots = 3,
    SelectOption = 4
}
