using System.Data;

namespace JobScheduler.DAL.Connection;

public interface IDbConnectionFactory
{
    Task<IDbConnection> GetWriteConnectionAsync();
    Task<IDbConnection> GetReadConnectionAsync();
}
