using System;
using System.Runtime.Serialization;

namespace ToDoist
{
    /// <summary>
    /// Одна задача списка. Поле Date хранится строкой (yyyy-MM-dd) —
    /// так JSON остаётся читаемым и не зависит от культуры.
    /// </summary>
    [DataContract]
    public class TaskItem
    {
        [DataMember(Name = "id", Order = 0)]
        public string Id;

        [DataMember(Name = "title", Order = 1)]
        public string Title;

        [DataMember(Name = "done", Order = 2)]
        public bool Done;

        /// <summary>День, к которому относится задача: yyyy-MM-dd.</summary>
        [DataMember(Name = "date", Order = 3)]
        public string Date;

        /// <summary>Когда задача создана: yyyy-MM-ddTHH:mm:ss.</summary>
        [DataMember(Name = "created", Order = 4)]
        public string Created;

        public static TaskItem Create(string title, DateTime now)
        {
            TaskItem item = new TaskItem();
            item.Id = Guid.NewGuid().ToString("N");
            item.Title = title == null ? string.Empty : title.Trim();
            item.Done = false;
            item.Date = now.ToString("yyyy-MM-dd");
            item.Created = now.ToString("yyyy-MM-ddTHH:mm:ss");
            return item;
        }
    }
}
