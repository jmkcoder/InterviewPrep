namespace Interview1.Worker.QueueService.Utilities
{
    public interface ITaskFactory
    {
        BaseTask GetTaskByTaskName(string name);
    }

    public class TaskFactory : ITaskFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public TaskFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public BaseTask GetTaskByTaskName(string name)
        {
            var taskType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.GetCustomAttributes(typeof(TaskAttribute), true)
                    .Cast<TaskAttribute>()
                    .Any(attr => attr.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    && typeof(BaseTask).IsAssignableFrom(type));
            if (taskType == null)
            {
                throw new ArgumentException($"No task found with name: {name}");
            }
            return (BaseTask)_serviceProvider.GetRequiredService(taskType);
        }
    }
}
