using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GamePrice.Api.Infrastructure
{
    public class DbConnection : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger<DbConnection> _logger;
        private SqlConnection? _connection;

        public DbConnection(IConfiguration configuration, ILogger<DbConnection> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' não configurada no appsettings.json");
            _logger = logger;
        }

        /// <summary>
        /// Obtém uma conexão aberta com o banco de dados.
        /// Reutiliza a conexão existente se já estiver aberta.
        /// </summary>
        public async Task<SqlConnection> GetConnectionAsync()
        {
            if (_connection is not null && _connection.State == ConnectionState.Open)
                return _connection;

            _connection = new SqlConnection(_connectionString);

            _logger.LogDebug("Abrindo conexão com o banco de dados...");
            await _connection.OpenAsync();
            _logger.LogDebug("Conexão aberta com sucesso");

            return _connection;
        }

        /// <summary>
        /// Cria uma nova conexão independente (para cenários de uso paralelo).
        /// O chamador é responsável por fechar/descartar essa conexão.
        /// </summary>
        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public void Dispose()
        {
            if (_connection is not null)
            {
                if (_connection.State != ConnectionState.Closed)
                {
                    _connection.Close();
                    _logger.LogDebug("Conexão com o banco de dados fechada");
                }

                _connection.Dispose();
                _connection = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
