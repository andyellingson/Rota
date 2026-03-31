using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rota.Services;

namespace Rota.Tests
{
    internal static class TestHelpers
    {
        /// <summary>
        /// Creates a WebApplicationFactory configured to replace <see cref="IUserService"/> with the provided instance.
        /// The caller is responsible for disposing the returned factory.
        /// </summary>
        public static WebApplicationFactory<Program> CreateFactoryWithUserService(IUserService userService)
        {
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace IUserService with the provided implementation for tests
                    services.AddSingleton<IUserService>(userService);
                });
            });

            return factory;
        }
    }
}
