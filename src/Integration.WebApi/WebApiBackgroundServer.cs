using System.Threading;
using System.Threading.Tasks;
using Vertica.Integration.Domain.LiteServer;
using Vertica.Integration.WebApi.Infrastructure;

namespace Vertica.Integration.WebApi
{
	internal class WebApiBackgroundServer : IBackgroundServer
	{
		private readonly IHttpServerFactory _factory;

		public WebApiBackgroundServer(IHttpServerFactory factory)
		{
			_factory = factory;
		}

		public Task Create(CancellationToken token)
		{
			return Task.Run(() =>
			{
				using (_factory.Create())
				{
					token.WaitHandle.WaitOne();
				}

			}, token);
		}
	}
}