using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace ToDoist
{
    /// <summary>
    /// Чтение и запись состояния в data.json.
    /// Запись атомарная: сначала временный файл, потом замена основного.
    /// </summary>
    public class Storage
    {
        private readonly string _directory;
        private readonly string _file;
        private readonly DataContractJsonSerializer _serializer;

        public Storage(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Не указана папка данных.", "directory");
            }

            _directory = directory;
            _file = Path.Combine(directory, "data.json");
            _serializer = new DataContractJsonSerializer(typeof(AppData));
        }

        public static string DefaultDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ToDoist");
            }
        }

        public string FilePath
        {
            get { return _file; }
        }

        public AppData Load()
        {
            try
            {
                if (!File.Exists(_file))
                {
                    return new AppData();
                }

                AppData data;
                using (FileStream stream = File.Open(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    data = (AppData)_serializer.ReadObject(stream);
                }

                if (data == null)
                {
                    throw new InvalidDataException("Файл данных пуст.");
                }

                data.Normalize();
                return data;
            }
            catch (Exception ex)
            {
                Log.Error("Не удалось прочитать " + _file + ". Файл сохранён как повреждённый.", ex);
                QuarantineCorruptedFile();
                return new AppData();
            }
        }

        public void Save(AppData data)
        {
            if (data == null)
            {
                return;
            }

            Directory.CreateDirectory(_directory);
            string temporary = _file + ".tmp";

            using (FileStream stream = File.Create(temporary))
            using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(
                stream, new UTF8Encoding(false), false, true, "  "))
            {
                _serializer.WriteObject(writer, data);
                writer.Flush();
            }

            if (File.Exists(_file))
            {
                try
                {
                    File.Replace(temporary, _file, null);
                }
                catch (Exception)
                {
                    File.Delete(_file);
                    File.Move(temporary, _file);
                }
            }
            else
            {
                File.Move(temporary, _file);
            }
        }

        private void QuarantineCorruptedFile()
        {
            try
            {
                if (!File.Exists(_file))
                {
                    return;
                }

                string quarantine = _file + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Move(_file, quarantine);
            }
            catch (Exception)
            {
                // Ничего страшного: просто начнём с чистого листа.
            }
        }
    }
}
