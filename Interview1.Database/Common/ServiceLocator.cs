using Microsoft.Extensions.DependencyInjection;

namespace Interview1.Database.Common
{
    public static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;

        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static T GetService<T>() where T : notnull
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceProvider has not been set. Call SetServiceProvider first.");

            return _serviceProvider.GetRequiredService<T>();
        }

        public static T? GetOptionalService<T>() where T : class
        {
            return _serviceProvider?.GetService<T>();
        }
    }
}