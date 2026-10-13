namespace XW.Infra.Repository;

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}
