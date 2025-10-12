using Interview1.Database.Common;
using Interview1.Database.Common.ColumnToProperty.Cache;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Interview1.Database
{
    public static class DatabaseServiceConfigurator
    {
        public static IServiceCollection AddDatabaseServices(this IServiceCollection services)
        {
            services.AddSingleton<IColumnToPropertyMapperCache, ColumnToPropertyMapperCache>();
            services.AddScoped<IDbContext, OracleDbContext>();
            
            return services;
        }

        public static IApplicationBuilder UseDatabase(this IApplicationBuilder app)
        {
            ServiceLocator.SetServiceProvider(app.ApplicationServices);
            
            return app;
        }
    }
}
