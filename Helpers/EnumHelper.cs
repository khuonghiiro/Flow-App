using FlowMy.Models;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace FlowMy.Helpers
{
    public static class EnumHelper
    {
        private static readonly ConcurrentDictionary<Type, ObservableCollection<EnumItem>> _enumCache
            = new ConcurrentDictionary<Type, ObservableCollection<EnumItem>>();

        public static ObservableCollection<EnumItem> GetEnumCollection<TEnum>(bool includeEmpty = true)
            where TEnum : struct, Enum
        {
            // LỖI 1: Sửa _enumCache thay vì *enumCache
            return _enumCache.GetOrAdd(typeof(TEnum), _ => CreateEnumCollection<TEnum>(includeEmpty));
        }

        private static ObservableCollection<EnumItem> CreateEnumCollection<TEnum>(bool includeEmpty)
            where TEnum : struct, Enum
        {
            var collection = new ObservableCollection<EnumItem>();

            if (includeEmpty)
            {
                collection.Add(new EnumItem(null, "Empty", "--- Chọn ---"));
            }

            foreach (TEnum enumValue in Enum.GetValues<TEnum>())
            {
                // THAY ĐỔI: Lưu enum value thay vì int
                var name = enumValue.ToString();
                var displayName = GetDisplayName(enumValue);
                collection.Add(new EnumItem(enumValue, name, displayName)); // enumValue thay vì Convert.ToInt32
            }

            return collection;
        }

        private static string GetDisplayName<TEnum>(TEnum enumValue) where TEnum : struct, Enum
        {
            var memberInfo = typeof(TEnum).GetMember(enumValue.ToString()).FirstOrDefault();

            // LỖI 2: Phải dùng DescriptionAttribute thay vì DisplayNameAttribute
            var descriptionAttribute = memberInfo?.GetCustomAttribute<DescriptionAttribute>();
            return descriptionAttribute?.Description ?? enumValue.ToString();
        }

        public static TEnum SafeConvertToEnum<TEnum>(object value) where TEnum : struct, Enum
        {
            if (value == null) return default;
            try
            {
                if (value is TEnum enumValue) return enumValue;
                if (value is int intValue) return (TEnum)Enum.ToObject(typeof(TEnum), intValue);
                if (value is string strValue && int.TryParse(strValue, out int parsedInt))
                    return (TEnum)Enum.ToObject(typeof(TEnum), parsedInt);
            }
            catch { }
            return default;
        }

        public static EnumItem GetSelectedItem<TEnum>(ObservableCollection<EnumItem> collection, TEnum enumValue)
            where TEnum : struct, Enum
        {
            var intValue = Convert.ToInt32(enumValue);
            return collection.FirstOrDefault(item => Equals(item.Value, intValue));
        }

        // THÊM: Method để test kết quả
        public static void DebugEnumCollection<TEnum>(bool includeEmpty = true) where TEnum : struct, Enum
        {
            var collection = GetEnumCollection<TEnum>(includeEmpty);
            Console.WriteLine($"=== Debug {typeof(TEnum).Name} ===");
            foreach (var item in collection)
            {
                Console.WriteLine($"Value: {item.Value ?? "null"}, Name: {item.Name}, Display: {item.DisplayName}");
            }
        }
    }
}
