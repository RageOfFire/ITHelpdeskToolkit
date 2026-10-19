using System;
using System.Threading.Tasks;

namespace ITHelpdeskToolkit.Models
{
    public enum RepairStatus
    {
        NotRun,
        Running,
        Done,
        Failed
    }

    public class RepairActionItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Func<Task<(bool Success, string Output)>> Action { get; set; } = null!;
        public bool RequiresAdmin { get; set; }
        public RepairStatus Status { get; set; } = RepairStatus.NotRun;
        public string LastOutput { get; set; } = string.Empty;

        public RepairActionItem() { }

        public RepairActionItem(int id, string title, string description, Func<Task<(bool Success, string Output)>> action, bool requiresAdmin)
        {
            Id = id;
            Title = title;
            Description = description;
            Action = action;
            RequiresAdmin = requiresAdmin;
        }
    }
}
