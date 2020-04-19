using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DominoCredentialsProxy.Service.ProxyUtils
{
	public class DominoClient
	{
		private TcpClient _tcpClient;
		private Task<TcpClient> _tcpClientCreateTask;

		public void CreateDominoConnection(string fromHeaderValue, CancellationToken ctx)
		{
			_tcpClientCreateTask = new Func<Task<TcpClient>>(async () =>
			{
				_tcpClient = new TcpClient();
				using (ctx.Register(() => _tcpClient.Close()))
				{
					// ^^^ _tcpClient.ConnectAsync();
				}

				return _tcpClient;
			}).Invoke();
		}
	}
}