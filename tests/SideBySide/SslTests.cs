using System.IO;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Xunit;

namespace SideBySide
{
	public class SslTests : IClassFixture<DatabaseFixture>
	{
		public SslTests(DatabaseFixture database)
		{
			m_database = database;
		}

		[SkippableFact(ConfigSettings.RequiresSsl)]
		public async Task ConnectSslPreferred()
		{
			var csb = AppConfig.CreateConnectionStringBuilder();
			csb.SslMode = MySqlSslMode.Preferred;
			csb.CertificateFile = null;
			csb.CertificatePassword = null;
			using (var connection = new MySqlConnection(csb.ConnectionString))
			{
				using (var cmd = connection.CreateCommand())
				{
					await connection.OpenAsync();
#if !BASELINE
					Assert.True(connection.SslIsEncrypted);
					Assert.True(connection.SslIsSigned);
					Assert.True(connection.SslIsAuthenticated);
					Assert.False(connection.SslIsMutuallyAuthenticated);
#endif
					cmd.CommandText = "SHOW SESSION STATUS LIKE 'Ssl_version'";
					var sslVersion = (string)await cmd.ExecuteScalarAsync();
					Assert.False(string.IsNullOrWhiteSpace(sslVersion));
				}
			}
		}

		[SkippableTheory(ConfigSettings.RequiresSsl | ConfigSettings.KnownClientCertificate)]
		[InlineData("ssl-client.pfx", null, null)]
		[InlineData("ssl-client-pw-test.pfx", "test", null)]
#if !BASELINE
		[InlineData("ssl-client.pfx", null, "ssl-ca-cert.pem")]
		[InlineData("ssl-client-pw-test.pfx", "test", "ssl-ca-cert.pem")]
#endif
		public async Task ConnectSslClientCertificate(string certFile, string certFilePassword, string caCertFile)
		{
			var csb = AppConfig.CreateConnectionStringBuilder();
			csb.CertificateFile = Path.Combine(AppConfig.CertsPath, certFile);
			csb.CertificatePassword = certFilePassword;
			if (caCertFile != null)
			{
				csb.SslMode = MySqlSslMode.VerifyCA;
#if !BASELINE
				csb.CACertificateFile = Path.Combine(AppConfig.CertsPath, caCertFile);
#endif
			}
			using (var connection = new MySqlConnection(csb.ConnectionString))
			{
				using (var cmd = connection.CreateCommand())
				{
					await connection.OpenAsync();
#if !BASELINE
					Assert.True(connection.SslIsEncrypted);
					Assert.True(connection.SslIsSigned);
					Assert.True(connection.SslIsAuthenticated);
					Assert.True(connection.SslIsMutuallyAuthenticated);
#endif
					cmd.CommandText = "SHOW SESSION STATUS LIKE 'Ssl_version'";
					var sslVersion = (string)await cmd.ExecuteScalarAsync();
					Assert.False(string.IsNullOrWhiteSpace(sslVersion));
				}
			}
		}

		[SkippableFact(ConfigSettings.RequiresSsl, Baseline = "MySql.Data does not check for a private key")]
		public async Task ConnectSslClientCertificateNoPrivateKey()
		{
			var csb = AppConfig.CreateConnectionStringBuilder();
			csb.CertificateFile = Path.Combine(AppConfig.CertsPath, "ssl-client-cert.pem");
			csb.SslMode = MySqlSslMode.Required;
			using (var connection = new MySqlConnection(csb.ConnectionString))
			{
				await Assert.ThrowsAsync<MySqlException>(async () => await connection.OpenAsync());
			}
		}

		[SkippableFact(ServerFeatures.KnownCertificateAuthority, ConfigSettings.RequiresSsl)]
		public async Task ConnectSslBadClientCertificate()
		{
			var csb = AppConfig.CreateConnectionStringBuilder();
			csb.CertificateFile = Path.Combine(AppConfig.CertsPath, "non-ca-client.pfx");
			csb.CertificatePassword = "";
			using (var connection = new MySqlConnection(csb.ConnectionString))
			{
#if BASELINE
				var exType = typeof(IOException);
#else
				var exType = typeof(MySqlException);
#endif
				await Assert.ThrowsAsync(exType, async () => await connection.OpenAsync());
			}
		}

		[SkippableFact(ServerFeatures.KnownCertificateAuthority, ConfigSettings.RequiresSsl, Baseline = "MySql.Data does not support CACertificateFile")]
		public async Task ConnectSslBadCaCertificate()
		{
			var csb = AppConfig.CreateConnectionStringBuilder();
			csb.CertificateFile = Path.Combine(AppConfig.CertsPath, "ssl-client.pfx");
			csb.SslMode = MySqlSslMode.VerifyCA;
#if !BASELINE
			csb.CACertificateFile = Path.Combine(AppConfig.CertsPath, "non-ca-client-cert.pem");
#endif
			using (var connection = new MySqlConnection(csb.ConnectionString))
			{
				await Assert.ThrowsAsync<MySqlException>(async () => await connection.OpenAsync());
			}
		}

		readonly DatabaseFixture m_database;
	}
}
