namespace XW.Infra.Core.Exceptions;

[Serializable]
public class BusinessException(string message) : Exception(message), IXWException
{
    public int Status { get; set; } = 521;
}
