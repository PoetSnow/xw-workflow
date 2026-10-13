using XW.Agent.Application.Models;
using XW.Agent.Application.Runtime.Pending;

namespace XW.Agent.Application.Runtime;

/// <summary>
/// 兼容门面：委托给 Pending 模块。新代码请直接用 <see cref="PendingTaskSnapshot"/> /
/// <see cref="PendingTaskClassifier"/> / <see cref="IPendingTaskDispatcher"/>。
/// </summary>
public static class PendingTaskState
{
    public const string SlotKind = PendingTaskKinds.SlotKind;
    public const string SlotStep = PendingTaskKinds.SlotStep;
    public const string KindPurchaseCreate = PendingTaskKinds.KindPurchaseCreate;
    public const string StepAwaitConfirm = PendingTaskKinds.StepAwaitConfirm;
    public const string StepAwaitSupplierPrice = PendingTaskKinds.StepAwaitSupplierPrice;
    public const string StepAwaitFinalConfirm = PendingTaskKinds.StepAwaitFinalConfirm;

    public enum UserReplyKind
    {
        Unrelated = 0,
        Affirm = 1,
        Cancel = 2,
        ProvideSlots = 3,
        SelectOption = 4
    }

    public static bool HasPending(AgentSession session) => PendingTaskSnapshot.HasPending(session);

    public static string? GetKind(AgentSession session) => PendingTaskSnapshot.GetKind(session);

    public static string? GetStep(AgentSession session) => PendingTaskSnapshot.GetStep(session);

    public static void Set(AgentSession session, string kind, string step) =>
        PendingTaskSnapshot.Set(session, kind, step);

    public static void Clear(AgentSession session) => PendingTaskSnapshot.ClearPurchase(session);

    public static void ArmPurchaseAfterLowStock(AgentSession session) =>
        PendingTaskSnapshot.ArmPurchaseAfterLowStock(session);

    public static void ArmAwaitSupplierPrice(AgentSession session) =>
        PendingTaskSnapshot.ArmAwaitSupplierPrice(session);

    public static UserReplyKind ClassifyReply(AgentSession session, string message) =>
        (UserReplyKind)(int)PendingTaskClassifier.Classify(session, message);

    public static bool TryParseSupplierPrice(string text, out string? supplierId, out string? unitPrice) =>
        PendingTaskClassifier.TryParseSupplierPrice(text, out supplierId, out unitPrice);
}
