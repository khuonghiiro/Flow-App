namespace FlowMy.Models
{

    public class EnumItem
    {
        public object Value { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }

        public EnumItem(object value, string name, string displayName)
        {
            Value = value;
            Name = name;
            DisplayName = displayName;
        }

        // Override ToString để hiển thị text không có space khi được chọn
        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj) => obj is EnumItem item && Equals(Value, item.Value);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    }
}
