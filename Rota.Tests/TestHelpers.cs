using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rota.Services;

namespace Rota.Tests
{
    internal static class TestHelpers
    {
        /// <summary>
        /// Creates a WebApplicationFactory configured to replace <see cref="IUserService"/> and
        /// optionally <see cref="ISchedulesService"/> with provided test instances.
        /// The caller is responsible for disposing the returned factory.
        /// </summary>
        public static WebApplicationFactory<Program> CreateFactoryWithUserService(IUserService userService, ISchedulesService? schedulesService = null)
        {
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace IUserService with the provided implementation for tests.
                    services.AddSingleton<IUserService>(userService);

                    if (schedulesService is not null)
                    {
                        // Register a test schedules service when a test needs to exercise register-link flows.
                        services.AddSingleton<ISchedulesService>(schedulesService);
                    }
                });
            });

            return factory;
        }
    }
}
