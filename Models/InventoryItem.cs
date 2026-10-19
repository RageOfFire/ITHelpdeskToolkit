namespace ITHelpdeskToolkit.Models
{
    public class InventoryItem
    {
        public string Property { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public InventoryItem() { }

        public InventoryItem(string property, string value)
        {
            Property = property;
            Value = value;
        }
    }
}
