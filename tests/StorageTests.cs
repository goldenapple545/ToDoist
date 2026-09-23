using System;
using System.Collections.Generic;
using System.IO;

namespace ToDoist.Tests
{
    /// <summary>
    /// Самопроверка слоя хранения данных (без WPF).
    /// Запуск: bin\StorageTests.exe — код возврата 0, если всё прошло.
    /// </summary>
    internal static class StorageTests
    {
        internal static int _passed;
        internal static int _failed;

        private static int Main()
        {
            Log.Silent = true;

            string dir = Path.Combine(Path.GetTempPath(), "todoist-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            Console.WriteLine("Проверка хранилища: " + dir);
            Console.WriteLine();

            try
            {
                TestMissingFile(dir);
                TestRoundTrip(dir);
                TestAtomicWrite(dir);
                TestCorruptFile(dir);
                TestNormalize(dir);
                BackdropTests.Run();
                TextShadowTests.Run();
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[ПАДЕНИЕ] Неожиданное исключение: " + ex);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch (Exception)
                {
                }
            }

            Console.WriteLine();
            Console.WriteLine(string.Format("Итого: {0} пройдено, {1} провалено.", _passed, _failed));
            return _failed == 0 ? 0 : 1;
        }

        private static void TestMissingFile(string dir)
        {
            Storage storage = new Storage(Path.Combine(dir, "case-missing"));
            AppData data = storage.Load();

            Check("нет файла: список задач пуст", data.Tasks.Count == 0);
            Check("нет файла: прозрачность по умолчанию",
                Math.Abs(data.Opacity - AppData.DefaultOpacity) < 0.0001);
            Check("нет файла: тема auto", data.Theme == AppData.ThemeAuto);
            Check("нет файла: геометрия по умолчанию",
                Math.Abs(data.Width - AppData.DefaultWidth) < 0.0001 &&
                Math.Abs(data.Height - AppData.DefaultHeight) < 0.0001);
        }

        private static void TestRoundTrip(string dir)
        {
            string caseDir = Path.Combine(dir, "case-roundtrip");
            Storage storage = new Storage(caseDir);

            DateTime now = new DateTime(2026, 9, 22, 10, 30, 0);
            AppData data = new AppData();
            data.Theme = AppData.ThemeDark;
            data.Opacity = 0.75;
            data.Topmost = true;
            data.HasBounds = true;
            data.Left = 120.5;
            data.Top = 60.25;
            data.Width = 420;
            data.Height = 640;
            data.Tasks.Add(TaskItem.Create("Купить молоко", now));
            data.Tasks.Add(TaskItem.Create("Позвонить в банк", now));
            data.Tasks.Add(TaskItem.Create("Прочитать главу книги", now));
            data.Tasks[1].Done = true;
            data.Tasks[0].Title = "  Купить молоко  ";

            storage.Save(data);
            Check("round-trip: файл создан", File.Exists(storage.FilePath));

            AppData loaded = storage.Load();

            Check("round-trip: три задачи", loaded.Tasks.Count == 3);
            Check("round-trip: заголовок с пробелами обрезан",
                loaded.Tasks[0].Title == "Купить молоко");
            Check("round-trip: отметка о выполнении сохранена",
                loaded.Tasks[1].Done && !loaded.Tasks[0].Done);
            Check("round-trip: дата задачи сохранена", loaded.Tasks[2].Date == "2026-09-22");
            Check("round-trip: тема сохранена", loaded.Theme == AppData.ThemeDark);
            Check("round-trip: прозрачность сохранена", Math.Abs(loaded.Opacity - 0.75) < 0.0001);
            Check("round-trip: поверх всех окон", loaded.Topmost);
            Check("round-trip: позиция окна", loaded.HasBounds &&
                Math.Abs(loaded.Left - 120.5) < 0.0001 && Math.Abs(loaded.Top - 60.25) < 0.0001);
            Check("round-trip: размер окна", Math.Abs(loaded.Width - 420) < 0.0001 &&
                Math.Abs(loaded.Height - 640) < 0.0001);
            Check("round-trip: счётчики", loaded.RemainingCount == 2 && loaded.DoneCount == 1);

            string json = File.ReadAllText(storage.FilePath);
            Check("round-trip: JSON читаемый (с отступами)", json.Contains(Environment.NewLine));
            Check("round-trip: кириллица в JSON не экранирована", json.Contains("Купить молоко"));

            loaded.Tasks.RemoveAt(0);
            storage.Save(loaded);
            AppData second = storage.Load();
            Check("повторная запись: осталось две задачи", second.Tasks.Count == 2);
        }

        private static void TestAtomicWrite(string dir)
        {
            string caseDir = Path.Combine(dir, "case-atomic");
            Storage storage = new Storage(caseDir);
            AppData data = new AppData();
            data.Tasks.Add(TaskItem.Create("Раз", DateTime.Now));

            storage.Save(data);
            Check("атомарная запись: временный файл удалён",
                !File.Exists(Path.Combine(caseDir, "data.json.tmp")));
            Check("атомарная запись: основной файл на месте", File.Exists(storage.FilePath));

            data.Tasks.Add(TaskItem.Create("Два", DateTime.Now));
            storage.Save(data);
            Check("атомарная запись: повторное сохранение прошло",
                storage.Load().Tasks.Count == 2);
            Check("атомарная запись: мусора не осталось",
                Directory.GetFiles(caseDir, "*.tmp").Length == 0);
        }

        private static void TestCorruptFile(string dir)
        {
            string caseDir = Path.Combine(dir, "case-corrupt");
            Directory.CreateDirectory(caseDir);
            string file = Path.Combine(caseDir, "data.json");
            File.WriteAllText(file, "{ это точно не JSON ");

            Storage storage = new Storage(caseDir);
            AppData data = storage.Load();

            Check("битый файл: приложение не падает", data != null);
            Check("битый файл: данные сброшены", data.Tasks.Count == 0);
            Check("битый файл: повреждённый файл отложен",
                Directory.GetFiles(caseDir, "data.json.corrupt-*").Length == 1);
            Check("битый файл: рабочий файл убран", !File.Exists(file));
        }

        private static void TestNormalize(string dir)
        {
            string caseDir = Path.Combine(dir, "case-normalize");
            Directory.CreateDirectory(caseDir);
            string file = Path.Combine(caseDir, "data.json");
            File.WriteAllText(file,
                "{\"version\":1,\"theme\":\"неон\",\"opacity\":42,\"width\":-10,\"height\":99999," +
                "\"top\":0,\"tasks\":[{\"title\":\"   \"},{\"title\":\"Дело\",\"date\":\"\"}]}");

            Storage storage = new Storage(caseDir);
            AppData data = storage.Load();

            Check("нормализация: неверная тема сброшена", data.Theme == AppData.ThemeAuto);
            Check("нормализация: прозрачность в границах",
                data.Opacity >= AppData.MinOpacity && data.Opacity <= AppData.MaxOpacity);
            Check("нормализация: размеры восстановлены",
                data.Width >= AppData.MinWidth && data.Height >= AppData.MinHeight);
            Check("нормализация: задача без текста удалена", data.Tasks.Count == 1);
            Check("нормализация: пустая дата заполнена",
                data.Tasks.Count == 1 && !string.IsNullOrEmpty(data.Tasks[0].Date));
            Check("нормализация: id сгенерирован",
                data.Tasks.Count == 1 && !string.IsNullOrEmpty(data.Tasks[0].Id));
        }

        internal static void Check(string name, bool condition)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("  [OK]   " + name);
            }
            else
            {
                _failed++;
                Console.WriteLine("  [FAIL] " + name);
            }
        }
    }
}
