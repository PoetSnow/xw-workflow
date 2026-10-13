namespace XW.Approve.Domain.Exceptions;

/// <summary>审批领域规则被违反时抛出。</summary>
public sealed class ApprovalDomainException(string message) : InvalidOperationException(message);
