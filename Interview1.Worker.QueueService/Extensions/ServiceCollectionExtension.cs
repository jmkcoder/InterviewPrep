using Interview1.Worker.QueueService.Middleware;
using Interview1.Worker.QueueService.Utilities;

namespace Interview1.Worker.QueueService.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddTasks(this IServiceCollection services)
        {
            services.AddSingleton<ITaskFactory, Utilities.TaskFactory>();

            var taskTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.GetCustomAttributes(typeof(TaskAttribute), true).Length > 0
                               && typeof(BaseTask).IsAssignableFrom(type));

            foreach (var taskType in taskTypes)
            {
                services.AddTransient(taskType);
            }
            
            return services;
        }

        /// <summary>
        /// Adds task processing services including tasks and middleware.
        /// </summary>
        public static IServiceCollection AddTaskProcessing(this IServiceCollection services)
        {
            services.AddTasks();
            services.AddTaskMiddleware();
            return services;
        }
    }
}
