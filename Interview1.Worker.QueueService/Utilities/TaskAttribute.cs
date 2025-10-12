namespace Interview1.Worker.QueueService.Utilities
{
    public class TaskAttribute : Attribute
    {
        public string Name { get; }

        public TaskAttribute(string name)
        {
            Name = name;
        }
    }
}