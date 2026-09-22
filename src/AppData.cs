using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ToDoist
{
    /// <summary>
    /// Всё состояние приложения: задачи, тема, прозрачность и геометрия окна.
    /// Лежит в %APPDATA%\ToDoist\data.json.
    /// </summary>
    [DataContract]
    public class AppData
    {
        public const string ThemeAuto = "auto";
        public const string ThemeLight = "light";
        public const string ThemeDark = "dark";

        public const double MinOpacity = 0.45;
        public const double MaxOpacity = 1.0;
        public const double DefaultOpacity = 0.90;

        public const double MinWidth = 300.0;
        public const double MinHeight = 380.0;
        public const double DefaultWidth = 380.0;
        public const double DefaultHeight = 560.0;

        [DataMember(Name = "version", Order = 0)]
        public int Version = 1;

        /// <summary>auto | light | dark</summary>
        [DataMember(Name = "theme", Order = 1)]
        public string Theme = ThemeAuto;

        /// <summary>Прозрачность «стекла», 0.45 .. 1.0</summary>
        [DataMember(Name = "opacity", Order = 2)]
        public double Opacity = DefaultOpacity;

        [DataMember(Name = "topmost", Order = 3)]
        public bool Topmost;

        [DataMember(Name = "hasBounds", Order = 4)]
        public bool HasBounds;

        [DataMember(Name = "left", Order = 5)]
        public double Left;

        [DataMember(Name = "top", Order = 6)]
        public double Top;

        [DataMember(Name = "width", Order = 7)]
        public double Width = DefaultWidth;

        [DataMember(Name = "height", Order = 8)]
        public double Height = DefaultHeight;

        [DataMember(Name = "tasks", Order = 9)]
        public List<TaskItem> Tasks = new List<TaskItem>();

        /// <summary>
        /// Приводит состояние к допустимому виду после чтения файла.
        /// </summary>
        public void Normalize()
        {
            if (Tasks == null)
            {
                Tasks = new List<TaskItem>();
            }

            DateTime now = DateTime.Now;
            foreach (TaskItem task in Tasks.ToArray())
            {
                // Заголовок из одних пробелов считаем пустым и выбрасываем задачу.
                if (task == null || string.IsNullOrWhiteSpace(task.Title))
                {
                    Tasks.Remove(task);
                    continue;
                }

                task.Title = task.Title.Trim();

                if (string.IsNullOrEmpty(task.Id))
                {
                    task.Id = Guid.NewGuid().ToString("N");
                }

                if (string.IsNullOrEmpty(task.Date))
                {
                    task.Date = now.ToString("yyyy-MM-dd");
                }

                if (string.IsNullOrEmpty(task.Created))
                {
                    task.Created = now.ToString("yyyy-MM-ddTHH:mm:ss");
                }
            }

            if (Opacity < MinOpacity || Opacity > MaxOpacity)
            {
                Opacity = DefaultOpacity;
            }

            if (Width < MinWidth || Width > 4000)
            {
                Width = DefaultWidth;
            }

            if (Height < MinHeight || Height > 4000)
            {
                Height = DefaultHeight;
            }

            if (Theme != ThemeLight && Theme != ThemeDark)
            {
                Theme = ThemeAuto;
            }

            if (double.IsNaN(Left) || double.IsInfinity(Left))
            {
                Left = 0;
                HasBounds = false;
            }

            if (double.IsNaN(Top) || double.IsInfinity(Top))
            {
                Top = 0;
                HasBounds = false;
            }
        }

        public int RemainingCount
        {
            get
            {
                int count = 0;
                foreach (TaskItem task in Tasks)
                {
                    if (!task.Done)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int DoneCount
        {
            get
            {
                int count = 0;
                foreach (TaskItem task in Tasks)
                {
                    if (task.Done)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
    }
}
