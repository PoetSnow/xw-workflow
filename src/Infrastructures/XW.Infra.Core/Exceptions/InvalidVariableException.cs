namespace XW.Infra.Core.Exceptions;

[Serializable]
public class InvalidVariableException(string message) : Exception(message), IXWException
{
    public int Status { get; set; } = 520;
}
